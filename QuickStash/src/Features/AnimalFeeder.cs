using System;
using System.Collections.Generic;
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
    /// </summary>
    internal static class AnimalFeeder
    {
        private static readonly List<Container> Buffer = new List<Container>(16);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(32);

        public static bool Active(MonsterAI ai)
        {
            if (!PluginConfig.Enabled.Value || !PluginConfig.FeedAnimals.Value || ai == null)
            {
                return false;
            }

            Tameable tameable = ai.m_tamable;
            return tameable != null && tameable.IsTamed() && tameable.IsHungry();
        }

        /// <summary>
        /// Devuelve la comida ya tirada en el piso para que el animal la coma, o null si no hay
        /// nada utilizable cerca.
        /// </summary>
        public static ItemDrop TakeFromContainer(MonsterAI ai)
        {
            long playerId = ContainerAccess.LocalPlayerId();
            Vector3 position = ai.transform.position;

            ContainerRegistry.Query(
                position,
                PluginConfig.AnimalFeedRange.Value,
                PluginConfig.MaxScanned.Value,
                playerId,
                Buffer);

            foreach (Container container in Buffer)
            {
                // Misma decision que en los hornos: nada de arrebatar propiedad en algo que
                // corre solo, sin que nadie lo pida.
                if (container.m_nview == null || !container.m_nview.IsOwner())
                {
                    continue;
                }

                Inventory source = container.GetInventory();
                if (source == null)
                {
                    continue;
                }

                ItemDrop dropped = TakeOneFrom(ai, container, source);
                if (dropped != null)
                {
                    return dropped;
                }
            }

            return null;
        }

        private static ItemDrop TakeOneFrom(MonsterAI ai, Container container, Inventory source)
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

                    return DropOne(ai, container, source, item);
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
            if (__result != null || !AnimalFeeder.Active(__instance))
            {
                return;
            }

            try
            {
                __result = AnimalFeeder.TakeFromContainer(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al dar de comer desde el cofre: {e}");
            }
        }
    }
}
