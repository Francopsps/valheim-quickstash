using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Core;
using UnityEngine;

namespace QuickStash.Features
{
    /// <summary>
    /// Los animales domesticados comen de un cofre cercano cuando no encuentran comida en el suelo.
    ///
    /// Como funciona el hambre en el vanilla (MonsterAI):
    ///
    ///     UpdateConsumeItem()  -> cada m_consumeSearchInterval (10 s), si m_tamable.IsHungry()
    ///                             llama a FindClosestConsumableItem(m_consumeSearchRange)
    ///     FindClosestConsumableItem() -> Physics.OverlapSphere sobre la capa "item": solo ve
    ///                                    objetos tirados en el SUELO
    ///     CanConsume(item)     -> compara contra m_consumeItems, la lista de comida propia de
    ///                             cada bicho
    ///
    /// El mod se engancha en el momento exacto en que el animal tiene hambre y no encontro nada:
    /// saca UNA unidad de comida del cofre y la tira al piso al lado del animal, y despues deja
    /// que el juego haga todo lo demas (caminar, la animacion de comer, resetear el hambre, el
    /// progreso de domesticacion). Por eso la regla de "solo lo que pueden comer normalmente"
    /// sale gratis: el filtro es el CanConsume del propio juego, no una lista nuestra.
    ///
    /// Se tira al lado del ANIMAL y no del cofre a proposito: el cofre suele estar del otro lado
    /// del cerco, y si la comida cayera ahi el animal no tendria camino hasta ella.
    ///
    /// Seguridad con 14 jugadores, igual que en los hornos:
    /// - BaseAI.UpdateAI sale temprano si !m_nview.IsOwner(), asi que toda esta rama corre solo
    ///   en el cliente dueno del animal: exactamente uno, sin coordinar nada.
    /// - Solo se usan cofres que ya son nuestros; no se le arrebata la propiedad a nadie.
    ///
    /// Diagnostico: con LogDeRendimiento activo se explica en el log POR QUE no se alimento, y
    /// se lista una vez el menu real de cada especie. Esa lista vive en los prefabs del juego,
    /// no en el codigo, asi que es la unica forma de saber que come cada bicho.
    /// </summary>
    internal static class AnimalFeeder
    {
        private static readonly List<Container> Buffer = new List<Container>(16);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(32);
        private static readonly HashSet<string> LoggedMenus = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder MenuBuilder = new StringBuilder(128);

        /// <summary>
        /// Devuelve la comida ya tirada en el piso para que el animal la coma, o null si no hay
        /// nada utilizable cerca.
        /// </summary>
        public static ItemDrop TryFeed(MonsterAI ai)
        {
            if (!PluginConfig.Enabled.Value || !PluginConfig.FeedAnimals.Value || ai == null)
            {
                return null;
            }

            Tameable tameable = ai.m_tamable;
            if (tameable == null || !tameable.IsTamed())
            {
                return null;
            }

            if (!tameable.IsHungry())
            {
                return null;
            }

            long playerId = ContainerAccess.LocalPlayerId();

            ContainerRegistry.Query(
                ai.transform.position,
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

                ItemDrop dropped = DropOne(ai, container, source, food);
                if (dropped != null)
                {
                    if (PluginConfig.DebugTiming.Value)
                    {
                        Plugin.Log.LogInfo(
                            $"[animal] {Name(ai)}: saco {MoveLog.Localize(food.m_shared.m_name)} " +
                            $"de un cofre a {Distance(ai, container):F1} m");
                    }

                    return dropped;
                }
            }

            Explain(ai, owned, withFood);
            return null;
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
        private static ItemDrop DropOne(MonsterAI ai, Container container, Inventory source, ItemDrop.ItemData item)
        {
            ItemDrop.ItemData single = item.Clone();
            single.m_stack = 1;

            if (!source.RemoveItem(item, 1))
            {
                return null;
            }

            // RemoveItem ya dispara Changed() adentro, que en un cofre propio termina en Save().

            Vector3 offset = UnityEngine.Random.insideUnitSphere * 0.3f;
            Vector3 position = ai.transform.position + new Vector3(offset.x, 0.4f, offset.z);

            ItemDrop dropped = ItemDrop.DropItem(single, 1, position, Quaternion.identity);
            if (dropped == null)
            {
                return null;
            }

            MoveLog.RecordAnimalFeed(container, item.m_shared.m_name, 1);
            return dropped;
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
                reason = $"no hay ningun cofre accesible a menos de {PluginConfig.AnimalFeedRange.Value:F0} m";
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
            if (ai.m_consumeItems != null)
            {
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
            }

            Plugin.Log.LogInfo(
                $"[animal] {name} come: {(MenuBuilder.Length > 0 ? MenuBuilder.ToString() : "(nada, lista vacia)")}");
            MenuBuilder.Length = 0;
        }

        private static string Name(MonsterAI ai)
        {
            Character character = ai.m_character;
            return character != null ? MoveLog.Localize(character.m_name) : ai.name;
        }

        private static float Distance(MonsterAI ai, Container container)
        {
            return Vector3.Distance(ai.transform.position, container.transform.position);
        }

        public static void Reset()
        {
            Buffer.Clear();
            ItemBuffer.Clear();
            LoggedMenus.Clear();
        }
    }

    /// <summary>
    /// Solo se engancha cuando el vanilla ya busco y no encontro comida en el suelo. Eso pasa
    /// como mucho una vez cada m_consumeSearchInterval (10 s por defecto) y unicamente si el
    /// animal tiene hambre, asi que no hace falta ningun temporizador propio.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), "FindClosestConsumableItem")]
    internal static class MonsterAI_FindClosestConsumableItem_Patch
    {
        private static void Postfix(MonsterAI __instance, ref ItemDrop __result)
        {
            if (__result != null)
            {
                return;
            }

            try
            {
                __result = AnimalFeeder.TryFeed(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al dar de comer desde el cofre: {e}");
            }
        }
    }
}
