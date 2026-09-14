using System;
using System.Collections.Generic;
using System.Text;
using QuickStash.Config;
using QuickStash.Core;
using UnityEngine;

namespace QuickStash.Features
{
    /// <summary>
    /// Los animales domesticados comen de un cofre cercano cuando no encuentran comida en el suelo.
    ///
    /// NO hace falta ser dueno del ZDO del animal, y ese es el punto de todo el diseno.
    ///
    /// El primer intento colgaba de un postfix de MonsterAI.FindClosestConsumableItem, y nunca
    /// se ejecutaba: ese metodo cuelga de UpdateConsumeItem &lt;- UpdateAI, y BaseAI.UpdateAI
    /// arranca con `if (!m_nview.IsOwner()) return false;`. En un servidor con varios jugadores
    /// la propiedad de los ZDO es pegajosa (ZDOMan.ReleaseNearbyZDOS solo reasigna si el dueno
    /// actual salio de su area activa), asi que el jugador parado al lado del corral tipicamente
    /// NO es el dueno de sus animales.
    ///
    /// Lo unico que hay que hacer es poner la comida en el suelo. De ahi en adelante, el cliente
    /// que si es dueno corre su FindClosestConsumableItem vanilla, ve el objeto tirado —que es un
    /// objeto del mundo, visible para todos— y se lo come, sin ningun parche de por medio. Como
    /// consecuencia, la funcion anda incluso si ese jugador no tiene el mod instalado.
    ///
    /// Limitacion de tasa en tres capas independientes, porque ninguna alcanza sola:
    ///  1. Cooldown por animal tras un tiro exitoso, de al menos lo que tarda el juego en volver
    ///     a buscar comida (m_consumeSearchInterval, 10 s).
    ///  2. No se tira si ya hay comida en el piso. Ojo: este chequeo puede truncarse, ver
    ///     HasFoodOnGround, asi que NO puede ser la unica defensa.
    ///  3. Tope de animales atendidos por ciclo.
    /// </summary>
    internal static class AnimalFeeder
    {
        /// <summary>Radio alrededor del jugador donde se buscan animales. Mas alla no estan ni cargados.</summary>
        private const float ScanRange = 40f;

        /// <summary>
        /// Espera minima entre dos tiros al mismo animal. Se usa el intervalo de busqueda del
        /// propio juego como piso: antes de eso el animal ni siquiera volvio a mirar el suelo.
        /// </summary>
        private const float MinFeedIntervalSeconds = 10f;

        /// <summary>
        /// Si dos jugadores estan a menos de esta diferencia de distancia del animal, la eleccion
        /// no se decide por distancia (sus posiciones replicadas tienen lag y cada cliente mide
        /// distinto) sino por un criterio que los 14 calculan igual.
        /// </summary>
        private const float ClosestEpsilon = 1.5f;

        /// <summary>
        /// El vanilla usa 128 en BaseAI.s_tempSphereOverlap. Con 32 este barrido se truncaba en
        /// corrales con basura tirada y el guard fallaba ABIERTO, que es el peor sentido posible.
        /// </summary>
        private static readonly Collider[] ItemHits = new Collider[256];

        private static readonly List<Container> Buffer = new List<Container>(16);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(32);
        private static readonly Dictionary<MonsterAI, float> NextFeed = new Dictionary<MonsterAI, float>();
        private static readonly List<MonsterAI> PruneBuffer = new List<MonsterAI>(8);
        private static readonly HashSet<string> LoggedMenus = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder MenuBuilder = new StringBuilder(128);

        private static float _nextRun;
        private static int _itemMask;

        public static void Tick()
        {
            if (!PluginConfig.Enabled.Value || !PluginConfig.FeedAnimals.Value)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextRun)
            {
                return;
            }

            _nextRun = now + PluginConfig.AnimalIntervalSeconds.Value;

            List<BaseAI> instances = BaseAI.m_instances;
            if (instances == null)
            {
                return;
            }

            PruneStale(now);

            Vector3 origin = player.transform.position;
            float sqrScan = ScanRange * ScanRange;
            long playerId = ContainerAccess.LocalPlayerId();
            int budget = PluginConfig.AnimalMaxPerCycle.Value;

            for (int i = 0; i < instances.Count && budget > 0; i++)
            {
                MonsterAI ai = instances[i] as MonsterAI;
                if (ai == null)
                {
                    continue;
                }

                // Descartes baratos primero: tipo, vida, distancia y estado. Recien despues los
                // caros (eleccion por cercania, barrido de fisica y consulta de cofres).
                Tameable tameable = ai.m_tamable;
                if (tameable == null || !tameable.IsTamed())
                {
                    continue;
                }

                // Un animal muerto sigue en m_instances hasta que se destruye el objeto, y su ZDO
                // sigue diciendo que tiene hambre. Tirarle comida a un cadaver es pura basura.
                Character character = ai.m_character;
                if (character == null || character.IsDead())
                {
                    continue;
                }

                Vector3 animalPosition = ai.transform.position;
                if ((animalPosition - origin).sqrMagnitude > sqrScan)
                {
                    continue;
                }

                if (NextFeed.TryGetValue(ai, out float allowedAt) && now < allowedAt)
                {
                    continue;
                }

                if (!tameable.IsHungry())
                {
                    continue;
                }

                if (ai.m_consumeItems == null || ai.m_consumeItems.Count == 0)
                {
                    continue;
                }

                if (!IsClosestPlayer(player, animalPosition))
                {
                    continue;
                }

                if (HasFoodOnGround(ai))
                {
                    continue;
                }

                if (Feed(ai, animalPosition, playerId))
                {
                    NextFeed[ai] = now + Mathf.Max(MinFeedIntervalSeconds, PluginConfig.AnimalIntervalSeconds.Value);
                    budget--;
                }
            }
        }

        /// <summary>
        /// Evita que los 14 clientes le tiren comida al mismo bicho. Con diferencias chicas la
        /// distancia no sirve como criterio: cada cliente mide su propia posicion exacta contra
        /// la posicion replicada y con lag de los demas, asi que dos jugadores casi equidistantes
        /// pueden creerse ambos el mas cercano de forma sostenida. Ahi decide el ID, que los 14
        /// calculan igual.
        /// </summary>
        private static bool IsClosestPlayer(Player local, Vector3 animalPosition)
        {
            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count <= 1)
            {
                return true;
            }

            float localDistance = Vector3.Distance(local.transform.position, animalPosition);
            long localId = local.GetPlayerID();

            foreach (Player other in players)
            {
                if (other == null || other == local)
                {
                    continue;
                }

                float otherDistance = Vector3.Distance(other.transform.position, animalPosition);
                float difference = otherDistance - localDistance;

                if (difference < -ClosestEpsilon)
                {
                    return false;
                }

                if (Mathf.Abs(difference) <= ClosestEpsilon && other.GetPlayerID() < localId)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Misma comprobacion que hace MonsterAI.FindClosestConsumableItem, sin el HavePath: ese
        /// haria pathfinding sobre una criatura que este cliente no simula.
        ///
        /// Si el barrido se satura se devuelve true (hay comida) en vez de false. Fallar abierto
        /// aca significa seguir tirando comida al piso indefinidamente, que es como se vacia un
        /// cofre sin que nadie se de cuenta.
        /// </summary>
        private static bool HasFoodOnGround(MonsterAI ai)
        {
            if (_itemMask == 0)
            {
                _itemMask = LayerMask.GetMask("item");
            }

            int count = Physics.OverlapSphereNonAlloc(
                ai.transform.position, ai.m_consumeSearchRange, ItemHits, _itemMask);

            if (count >= ItemHits.Length)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                Collider hit = ItemHits[i];
                if (hit == null || hit.attachedRigidbody == null)
                {
                    continue;
                }

                ItemDrop drop = hit.attachedRigidbody.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData?.m_shared == null)
                {
                    continue;
                }

                if (ai.CanConsume(drop.m_itemData))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Feed(MonsterAI ai, Vector3 animalPosition, long playerId)
        {
            ContainerRegistry.Query(
                animalPosition,
                PluginConfig.AnimalFeedRange.Value,
                PluginConfig.MaxScanned.Value,
                playerId,
                Buffer);

            int owned = 0;
            int withFood = 0;

            foreach (Container container in Buffer)
            {
                // Misma decision que en los hornos: nada de arrebatar propiedad en algo que
                // corre solo, sin que nadie lo pida.
                if (container.m_nview == null || !container.m_nview.IsOwner())
                {
                    continue;
                }

                owned++;

                Inventory source = container.GetInventory();
                if (source == null)
                {
                    continue;
                }

                ItemDrop.ItemData food = FindFood(ai, source);
                if (food == null)
                {
                    continue;
                }

                withFood++;

                if (DropOne(ai, container, source, food, animalPosition))
                {
                    if (PluginConfig.DebugTiming.Value)
                    {
                        Plugin.Log.LogInfo(
                            $"[animal] {Name(ai)}: se le dejo {MoveLog.Localize(food.m_shared.m_name)} " +
                            $"de un cofre a {Vector3.Distance(animalPosition, container.transform.position):F1} m");
                    }

                    return true;
                }
            }

            Explain(ai, owned, withFood);
            return false;
        }

        private static ItemDrop.ItemData FindFood(MonsterAI ai, Inventory source)
        {
            ItemBuffer.Clear();
            ItemBuffer.AddRange(source.GetAllItems());

            try
            {
                foreach (ItemDrop.ItemData item in ItemBuffer)
                {
                    if (item?.m_shared == null || item.m_stack <= 0)
                    {
                        continue;
                    }

                    // Sin prefab no se puede instanciar el objeto en el mundo.
                    if (item.m_dropPrefab == null)
                    {
                        continue;
                    }

                    if (PluginConfig.IsExcluded(item) || PluginConfig.IsExcludedForAnimals(item))
                    {
                        continue;
                    }

                    if (!ai.CanConsume(item))
                    {
                        continue;
                    }

                    return item;
                }
            }
            finally
            {
                ItemBuffer.Clear();
            }

            return null;
        }

        /// <summary>
        /// Se descuenta del cofre PRIMERO y recien despues se instancia la comida: asi el peor
        /// caso es perder una unidad, no duplicarla. ItemDrop.DropItem nunca devuelve null —o
        /// instancia o tira—, por eso la garantia la da el try/catch y no un chequeo de null.
        /// </summary>
        private static bool DropOne(MonsterAI ai, Container container, Inventory source, ItemDrop.ItemData item, Vector3 animalPosition)
        {
            ItemDrop.ItemData single = item.Clone();
            single.m_stack = 1;
            string itemName = item.m_shared.m_name;

            if (!source.RemoveItem(item, 1))
            {
                return false;
            }

            // RemoveItem ya dispara Changed() adentro, que en un cofre propio termina en Save().

            // Al lado del ANIMAL y no del cofre: el cofre suele estar del otro lado del cerco, y
            // ahi el animal no tendria camino hasta la comida.
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 0.3f;
            Vector3 position = animalPosition + new Vector3(offset.x, 0.4f, offset.z);

            try
            {
                ItemDrop.DropItem(single, 1, position, Quaternion.identity);
            }
            catch (Exception e)
            {
                // La unidad ya salio del cofre y no llego al mundo: queda el rastro para poder
                // investigarlo si alguien reporta un faltante.
                Plugin.Log.LogError(
                    $"Se perdio 1 x {MoveLog.Localize(itemName)} al dejarsela a {Name(ai)}: {e.Message}");
                return false;
            }

            MoveLog.RecordAnimalFeed(container, itemName, 1);
            return true;
        }

        private static void PruneStale(float now)
        {
            if (NextFeed.Count < 64)
            {
                return;
            }

            PruneBuffer.Clear();
            foreach (KeyValuePair<MonsterAI, float> entry in NextFeed)
            {
                if (entry.Key == null || now > entry.Value + 300f)
                {
                    PruneBuffer.Add(entry.Key);
                }
            }

            foreach (MonsterAI ai in PruneBuffer)
            {
                NextFeed.Remove(ai);
            }

            PruneBuffer.Clear();
        }

        /// <summary>
        /// Explica en el log por que un animal hambriento no comio. Sin esto la funcion es una
        /// caja negra: no hay forma de distinguir "no hay cofres cerca" de "el cofre es de otro
        /// jugador" de "esa comida no le gusta".
        /// </summary>
        private static void Explain(MonsterAI ai, int owned, int withFood)
        {
            if (!PluginConfig.DebugTiming.Value)
            {
                return;
            }

            string reason;
            if (Buffer.Count == 0)
            {
                reason = $"no hay ningun cofre accesible a menos de {PluginConfig.AnimalFeedRange.Value:F0} m del animal";
            }
            else if (owned == 0)
            {
                reason = $"hay {Buffer.Count} cofre(s) cerca pero ninguno es tuyo " +
                         "(en el servidor la propiedad del ZDO puede estar en otro jugador)";
            }
            else if (withFood == 0)
            {
                reason = $"hay {owned} cofre(s) tuyo(s) cerca, pero ninguno tiene comida que este animal acepte";
            }
            else
            {
                reason = "se encontro comida pero no se pudo sacar del cofre";
            }

            Plugin.Log.LogInfo($"[animal] {Name(ai)} tiene hambre y no comio: {reason}.");
            LogMenuOnce(ai);
        }

        /// <summary>
        /// Vuelca una sola vez por especie la lista real de comida (m_consumeItems). Vive en los
        /// prefabs del juego, no en el codigo, asi que es la unica forma de saber que acepta.
        /// </summary>
        private static void LogMenuOnce(MonsterAI ai)
        {
            string name = Name(ai);
            if (!LoggedMenus.Add(name))
            {
                return;
            }

            MenuBuilder.Length = 0;
            foreach (ItemDrop consumable in ai.m_consumeItems)
            {
                // Un mod de criaturas puede dejar entradas incompletas en la lista.
                if (consumable == null || consumable.m_itemData?.m_shared == null)
                {
                    continue;
                }

                if (MenuBuilder.Length > 0)
                {
                    MenuBuilder.Append(", ");
                }

                MenuBuilder.Append(MoveLog.Localize(consumable.m_itemData.m_shared.m_name));
            }

            Plugin.Log.LogInfo($"[animal] {name} come: {MenuBuilder}");
            MenuBuilder.Length = 0;
        }

        private static string Name(MonsterAI ai)
        {
            Character character = ai.m_character;
            return character != null ? MoveLog.Localize(character.m_name) : ai.name;
        }

        public static void Reset()
        {
            Buffer.Clear();
            ItemBuffer.Clear();
            NextFeed.Clear();
            PruneBuffer.Clear();
            LoggedMenus.Clear();
            _nextRun = 0f;
        }
    }
}
