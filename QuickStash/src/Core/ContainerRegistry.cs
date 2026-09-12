using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace QuickStash.Core
{
    /// <summary>
    /// Registro estatico de todos los Container vivos del cliente.
    ///
    /// Se alimenta con los parches de ciclo de vida de Container en vez de usar
    /// FindObjectsOfType, que recorre la escena completa y es inviable con 14 jugadores
    /// y miles de objetos cargados.
    /// </summary>
    internal static class ContainerRegistry
    {
        private static readonly List<Container> All = new List<Container>(256);

        /// <summary>Buffer reutilizado para ordenar por distancia sin generar basura cada consulta.</summary>
        private static readonly List<Entry> SortBuffer = new List<Entry>(64);

        private static readonly Comparison<Entry> ByDistance = (a, b) => a.SqrDistance.CompareTo(b.SqrDistance);

        public static int Count => All.Count;

        private struct Entry
        {
            public Container Container;
            public float SqrDistance;
        }

        public static void Register(Container container)
        {
            if (container != null)
            {
                All.Add(container);
            }
        }

        public static void Unregister(Container container)
        {
            All.Remove(container);
        }

        public static void Clear()
        {
            All.Clear();
        }

        /// <summary>
        /// Rellena <paramref name="results"/> con los cofres utilizables dentro del rango,
        /// ordenados del mas cercano al mas lejano y recortados a <paramref name="max"/>.
        ///
        /// De paso elimina del registro las entradas ya destruidas (poda perezosa): asi no
        /// hace falta una pasada de limpieza aparte ni parchear el descargado de zonas.
        /// </summary>
        public static void Query(Vector3 point, float range, int max, long playerId, List<Container> results)
        {
            results.Clear();
            SortBuffer.Clear();

            float sqrRange = range * range;

            for (int i = All.Count - 1; i >= 0; i--)
            {
                Container container = All[i];

                if (container == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                float sqrDistance = (container.transform.position - point).sqrMagnitude;
                if (sqrDistance > sqrRange)
                {
                    continue;
                }

                if (!ContainerAccess.CanUse(container, playerId))
                {
                    continue;
                }

                SortBuffer.Add(new Entry { Container = container, SqrDistance = sqrDistance });
            }

            SortBuffer.Sort(ByDistance);

            int take = Mathf.Min(max, SortBuffer.Count);
            for (int i = 0; i < take; i++)
            {
                results.Add(SortBuffer[i].Container);
            }

            SortBuffer.Clear();
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class Container_Awake_Patch
    {
        private static void Postfix(Container __instance)
        {
            ContainerRegistry.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Container), "OnDestroyed")]
    internal static class Container_OnDestroyed_Patch
    {
        private static void Postfix(Container __instance)
        {
            ContainerRegistry.Unregister(__instance);
        }
    }
}
