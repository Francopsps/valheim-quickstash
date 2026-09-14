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
    /// Todo lo necesario para decidir se puede leer sin ser dueno:
    /// - Tameable.IsHungry() sale de ZDOVars.s_tameLastFeeding, y los ZDO estan replicados.
    /// - MonsterAI.m_consumeItems es dato del prefab, no del ZDO.
    ///
    /// Coordinacion entre clientes, en dos capas:
    /// - Solo actua el jugador mas cercano al animal.
    /// - Antes de tirar se comprueba que no haya ya comida en el piso, igual que hace el juego.
    ///   Eso vuelve la funcion autolimitante y hace inofensivo un doble tiro si dos clientes se
    ///   pisan en el margen: la segunda unidad simplemente se come despues.
    /// </summary>
    internal static class AnimalFeeder
    {
        /// <summary>Radio alrededor del jugador donde se buscan animales. Mas alla no estan ni cargados.</summary>
        private const float ScanRange = 40f;

        private static readonly List<Container> Buffer = new List<Container>(16);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(32);
        private static readonly Collider[] ItemHits = new Collider[32];
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

                // Descartes baratos primero: tipo, distancia y estado. Recien despues los caros
                // (eleccion por cercania, barrido de fisica y consulta de cofres).
                Tameable tameable = ai.m_tamable;
                if (tameable == null || !tameable.IsTamed())
                {
                    continue;
                }

                Vector3 animalPosition = ai.transform.position;
                if ((animalPosition - origin).sqrMagnitude > sqrScan)
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
                    budget--;
                }
            }
        }

        /// <summary>
        /// Evita que los 14 clientes le tiren comida al mismo bicho. Es una eleccion aproximada
        /// —dos clientes pueden discrepar en el margen— y por eso no es la unica defensa: la
        /// comprobacion de comida en el piso cubre el resto.
        /// </summary>
        private static bool IsClosestPlayer(Player local, Vector3 animalPosition)
        {
            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count <= 1)
            {
                return true;
            }

            float localDistance = (local.transform.position - animalPosition).sqrMagnitude;

            foreach (Player other in players)
            {
                if (other == null || other == local)
                {
                    continue;
                }

                if ((other.transform.position - animalPosition).sqrMagnitude < localDistance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Misma comprobacion que hace MonsterAI.FindClosestConsumableItem, sin el HavePath: ese
        /// haria pathfinding sobre una criatura que este cliente no simula.
        /// </summary>
        private static bool HasFoodOnGround(MonsterAI ai)
        {
            if (_itemMask == 0)
            {
                _itemMask = LayerMask.GetMask("item");
            }

            int count = Physics.OverlapSphereNonAlloc(
                ai.transform.position, ai.m_consumeSearchRange, ItemHits, _itemMask);

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

                    if (PluginConfig.IsExcluded(item) || !ai.CanConsume(item))
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
        /// Se descuenta del cofre PRIMERO y recien despues se instancia la comida. Al reves, un
        /// fallo al instanciar dejaria el objeto en el cofre y tambien en el suelo. Asi el peor
        /// caso es perder una unidad de comida, que es preferible a duplicarla.
        /// </summary>
        private static bool DropOne(MonsterAI ai, Container container, Inventory source, ItemDrop.ItemData item, Vector3 animalPosition)
        {
            ItemDrop.ItemData single = item.Clone();
            single.m_stack = 1;

            if (!source.RemoveItem(item, 1))
            {
                return false;
            }

            // RemoveItem ya dispara Changed() adentro, que en un cofre propio termina en Save().

            // Al lado del ANIMAL y no del cofre: el cofre suele estar del otro lado del cerco, y
            // ahi el animal no tendria camino hasta la comida.
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 0.3f;
            Vector3 position = animalPosition + new Vector3(offset.x, 0.4f, offset.z);

            if (ItemDrop.DropItem(single, 1, position, Quaternion.identity) == null)
            {
                return false;
            }

            MoveLog.RecordAnimalFeed(container, item.m_shared.m_name, 1);
            return true;
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
                if (consumable == null)
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
            LoggedMenus.Clear();
            _nextRun = 0f;
        }
    }
}
