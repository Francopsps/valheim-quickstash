using System;
using System.Collections.Generic;
using QuickStash.Config;
using UnityEngine;

namespace QuickStash.Core
{
    /// <summary>
    /// Inventario virtual de los cofres cercanos: cuanto hay de cada material.
    ///
    /// Existe porque Player.HaveRequirementItems e InventoryGui.SetupRequirement se llaman
    /// en cada frame mientras el panel de crafteo esta abierto. Sin este cache, cada frame
    /// recorreria todos los cofres del rango, que es justo lo que no podemos permitirnos
    /// con 14 jugadores.
    /// </summary>
    internal static class ContainerIndex
    {
        private readonly struct ItemKey : IEquatable<ItemKey>
        {
            public readonly string Name;
            public readonly int Quality;

            public ItemKey(string name, int quality)
            {
                Name = name;
                Quality = quality;
            }

            public bool Equals(ItemKey other) => Quality == other.Quality && Name == other.Name;

            public override bool Equals(object obj) => obj is ItemKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Name?.GetHashCode() ?? 0) * 397) ^ Quality;
                }
            }
        }

        private static readonly Dictionary<ItemKey, int> Counts = new Dictionary<ItemKey, int>(64);

        /// <summary>
        /// Totales por nombre, sin separar calidad. Existe aparte de <see cref="Counts"/> porque
        /// InventoryGui.UpdateRecipe corre en cada frame y consultar el total no puede costar
        /// un barrido del diccionario.
        /// </summary>
        private static readonly Dictionary<string, int> Totals = new Dictionary<string, int>(64);

        private static readonly List<Container> Nearby = new List<Container>(32);

        private static Vector3 _point;
        private static float _range = -1f;
        private static float _time = float.NegativeInfinity;
        private static bool _valid;

        public static IReadOnlyList<Container> Containers => Nearby;

        public static void Invalidate()
        {
            _valid = false;
            Nearby.Clear();
            Counts.Clear();
            Totals.Clear();
        }

        public static void EnsureFresh(Vector3 point, float range, long playerId)
        {
            float now = Time.realtimeSinceStartup;
            float ttl = PluginConfig.CacheMs.Value / 1000f;

            // Se reutiliza mientras no haya pasado el TTL y el jugador no se haya movido mas de un metro.
            if (_valid &&
                Mathf.Approximately(_range, range) &&
                now - _time <= ttl &&
                (point - _point).sqrMagnitude <= 1f)
            {
                return;
            }

            // Tope de lectura, no de escritura: este indice solo se usa para saber cuanto hay
            // disponible, y leer cofres no le cuesta nada al servidor.
            ContainerRegistry.Query(point, range, PluginConfig.MaxScanned.Value, playerId, Nearby);

            Counts.Clear();
            Totals.Clear();
            foreach (Container container in Nearby)
            {
                Inventory inventory = container.GetInventory();
                if (inventory == null)
                {
                    continue;
                }

                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item?.m_shared == null || item.m_worldLevel < Game.m_worldLevel)
                    {
                        continue;
                    }

                    ItemKey key = new ItemKey(item.m_shared.m_name, item.m_quality);
                    Counts.TryGetValue(key, out int current);
                    Counts[key] = current + item.m_stack;

                    Totals.TryGetValue(item.m_shared.m_name, out int total);
                    Totals[item.m_shared.m_name] = total + item.m_stack;
                }
            }

            _point = point;
            _range = range;
            _time = now;
            _valid = true;
        }

        /// <summary>Cuanto hay en los cofres cercanos. quality &lt; 0 suma todas las calidades.</summary>
        public static int Count(string name, int quality)
        {
            if (quality >= 0)
            {
                Counts.TryGetValue(new ItemKey(name, quality), out int exact);
                return exact;
            }

            Totals.TryGetValue(name, out int anyQuality);
            return anyQuality;
        }
    }
}
