using System;
using System.Collections.Generic;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Core;
using UnityEngine;

namespace QuickStash.Features
{
    /// <summary>
    /// Carga sola las estaciones tipo Smelter con lo que haya en un cofre al lado.
    ///
    /// Un solo componente cubre fundicion, horno de carbon, alto horno, molino, rueca y
    /// refineria de eitr.
    ///
    /// Por que es seguro con 14 jugadores:
    ///
    /// - Smelter.Awake hace InvokeRepeating("UpdateSmelter", 1f, 1f) en todos los clientes, asi
    ///   que un postfix da la cadencia gratis: sin registro propio, sin timer y sin
    ///   FindObjectsOfType.
    /// - Solo actua el cliente que es dueno del ZDO del horno. Como Valheim le asigna la
    ///   propiedad al jugador mas cercano (ZDOMan.ReleaseNearbyZDOS), eso elige exactamente un
    ///   cliente por horno sin coordinar nada.
    /// - Siendo duenos, InvokeRPC se despacha local y sincronico (ZRoutedRpc.InvokeRoutedRPC
    ///   llama a HandleRoutedRPC directo cuando el destino es uno mismo), asi que sacar del
    ///   cofre y cargar el horno pasan en la misma pila.
    /// - Solo se usan cofres que ya son nuestros: al ser un ciclo automatico, arrebatar la
    ///   propiedad repetiria muchas veces por minuto el riesgo que un clic manual corre una vez.
    /// </summary>
    internal static class StationFeeder
    {
        private static readonly List<Container> Buffer = new List<Container>(16);
        private static readonly List<Container> DiagBuffer = new List<Container>(16);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(32);
        private static readonly Dictionary<Smelter, float> NextCheck = new Dictionary<Smelter, float>();
        private static readonly List<Smelter> PruneBuffer = new List<Smelter>(8);

        private static float _budgetWindowStart;
        private static int _stationsInWindow;

        public static void Tick(Smelter smelter)
        {
            if (!PluginConfig.Enabled.Value || !PluginConfig.FeedStations.Value)
            {
                return;
            }

            ZNetView nview = smelter.m_nview;
            if (nview == null || !nview.IsValid())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (NextCheck.TryGetValue(smelter, out float next) && now < next)
            {
                return;
            }

            NextCheck[smelter] = now + PluginConfig.FeedIntervalSeconds.Value;
            PruneStale(now);

            // Ruta barata y la mas frecuente: dos lecturas de ZDO y afuera.
            bool wantsFuel = smelter.m_fuelItem != null && smelter.m_maxFuel > 0 &&
                             smelter.GetFuel() < smelter.m_maxFuel;
            bool wantsOre = smelter.GetQueueSize() < smelter.m_maxOre;

            // El chequeo de propiedad va DESPUES del diagnostico a proposito: si el horno no es
            // nuestro salimos, y sin esto el log quedaria mudo justo en el caso que hay que poder
            // ver. Este postfix si corre en todos los clientes, a diferencia de la IA de los
            // animales.
            bool owner = nview.IsOwner();
            if (!owner || (!wantsFuel && !wantsOre))
            {
                Diagnose(smelter, owner, wantsFuel, wantsOre, 0);
                return;
            }

            // Tope global del cliente. El costo de red no son los RPC (van a un ZDO propio y no
            // salen de la maquina) sino el Save() de cada cofre drenado, que serializa el
            // inventario completo y replica el ZDO. Sin esta cota, la cantidad de estaciones es
            // un eje sin limite para una funcion que corre sola.
            if (!TakeGlobalBudget(now))
            {
                return;
            }

            long playerId = ContainerAccess.LocalPlayerId();
            ContainerRegistry.Query(
                smelter.transform.position,
                PluginConfig.FeedRange.Value,
                PluginConfig.MaxScanned.Value,
                playerId,
                Buffer);

            int budget = PluginConfig.FeedMaxPerCycle.Value;

            foreach (Container container in Buffer)
            {
                if (budget <= 0)
                {
                    break;
                }

                // Decision tomada: no se le arrebata la propiedad a nadie en un ciclo automatico.
                if (container.m_nview == null || !container.m_nview.IsOwner())
                {
                    continue;
                }

                budget -= FeedFrom(smelter, container, budget);
            }

            if (budget == PluginConfig.FeedMaxPerCycle.Value)
            {
                Diagnose(smelter, owner: true, wantsFuel, wantsOre, playerId);
            }
        }

        /// <summary>
        /// Explica en el log por que un horno no se cargo. Sin esto la funcion es una caja negra:
        /// no hay forma de distinguir "el horno no es tuyo" de "el cofre no es tuyo" de "el cofre
        /// esta marcado en uso" de "no tiene material".
        /// </summary>
        private static void Diagnose(Smelter smelter, bool owner, bool wantsFuel, bool wantsOre, long playerId)
        {
            if (!PluginConfig.DebugTiming.Value)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            // Solo lo que el jugador tiene a la vista: con muchas estaciones cargadas el log se
            // llenaria de hornos que no esta mirando.
            float distance = Vector3.Distance(smelter.transform.position, player.transform.position);
            if (distance > 30f)
            {
                return;
            }

            string name = MoveLog.Localize(smelter.m_name);

            if (!owner)
            {
                Plugin.Log.LogInfo(
                    $"[horno] {name} a {distance:F1} m | dueno del ZDO: NO -> este cliente no lo alimenta " +
                    "(lo tiene otro jugador o el servidor)");
                return;
            }

            if (!wantsFuel && !wantsOre)
            {
                Plugin.Log.LogInfo($"[horno] {name} a {distance:F1} m | lleno, no necesita nada");
                return;
            }

            ContainerRegistry.Query(
                smelter.transform.position,
                PluginConfig.FeedRange.Value,
                PluginConfig.MaxScanned.Value,
                playerId,
                DiagBuffer);

            int ownedChests = 0;
            foreach (Container container in DiagBuffer)
            {
                if (container.m_nview != null && container.m_nview.IsOwner())
                {
                    ownedChests++;
                }
            }

            int inRange = ContainerRegistry.LastInRange;
            int usable = ContainerRegistry.LastUsable;

            string reason;
            if (inRange == 0)
            {
                reason = $"no hay ningun cofre a menos de {PluginConfig.FeedRange.Value:F0} m";
            }
            else if (usable < inRange)
            {
                reason = $"de {inRange} cofre(s) cerca, {inRange - usable} quedaron descartados por " +
                         "permisos, ward, o por estar marcados EN USO (un cofre con la tapa levantada " +
                         "que nadie abrio tiene la marca trabada: rompelo y volvelo a poner)";
            }
            else if (ownedChests == 0)
            {
                reason = $"hay {usable} cofre(s) utilizable(s) pero ninguno es tuyo " +
                         "(en el servidor la propiedad del ZDO puede estar en otro jugador)";
            }
            else
            {
                reason = $"hay {ownedChests} cofre(s) tuyo(s) pero ninguno tiene material que este horno acepte";
            }

            Plugin.Log.LogInfo(
                $"[horno] {name} a {distance:F1} m | quiere {(wantsFuel ? "combustible" : "")}" +
                $"{(wantsFuel && wantsOre ? " y " : "")}{(wantsOre ? "mineral" : "")} | " +
                $"en rango {inRange} | utilizables {usable} | tuyos {ownedChests} -> {reason}");
        }

        private static bool TakeGlobalBudget(float now)
        {
            if (now - _budgetWindowStart >= 1f)
            {
                _budgetWindowStart = now;
                _stationsInWindow = 0;
            }

            if (_stationsInWindow >= PluginConfig.FeedMaxStationsPerSecond.Value)
            {
                return false;
            }

            _stationsInWindow++;
            return true;
        }

        private static int FeedFrom(Smelter smelter, Container container, int budget)
        {
            Inventory source = container.GetInventory();
            if (source == null)
            {
                return 0;
            }

            string fuelPrefab = smelter.m_fuelItem != null ? smelter.m_fuelItem.gameObject.name : null;
            int fed = 0;

            // Un solo Save() del cofre al final en vez de uno por unidad cargada.
            Action previousHandler = source.m_onChanged;
            source.m_onChanged = null;

            try
            {
                ItemBuffer.Clear();
                ItemBuffer.AddRange(source.GetAllItems());

                foreach (ItemDrop.ItemData item in ItemBuffer)
                {
                    if (fed >= budget)
                    {
                        break;
                    }

                    if (item?.m_shared == null || item.m_dropPrefab == null || item.m_stack <= 0)
                    {
                        continue;
                    }

                    // Un horno de carbon acepta varios tipos de madera y todos dan carbon igual:
                    // sin esto, un cofre con madera fina al lado se convierte solo, y es
                    // irreversible. ItemsExcluidos es la valvula para eso.
                    if (PluginConfig.IsExcluded(item))
                    {
                        continue;
                    }

                    string prefabName = item.m_dropPrefab.name;
                    bool isFuel = fuelPrefab != null && prefabName == fuelPrefab;

                    // IsItemAllowed compara contra m_conversion[].m_from.gameObject.name, o sea
                    // el nombre de prefab, no m_shared.m_name.
                    bool isOre = !isFuel && smelter.IsItemAllowed(prefabName);

                    if (!isFuel && !isOre)
                    {
                        continue;
                    }

                    fed += isFuel
                        ? FeedFuel(smelter, container, source, item, budget - fed)
                        : FeedOre(smelter, container, source, item, prefabName, budget - fed);
                }
            }
            finally
            {
                ItemBuffer.Clear();
                source.m_onChanged = previousHandler;

                // El guardado va DENTRO del finally: si algo tira despues de haber sacado
                // unidades, el cofre tiene que guardarse igual. Si no, la baja queda solo en
                // memoria, el proximo Load la revierte y el horno ya se cargo: duplicacion.
                if (fed > 0)
                {
                    source.Changed();
                }
            }

            return fed;
        }

        private static int FeedFuel(Smelter smelter, Container container, Inventory source, ItemDrop.ItemData item, int budget)
        {
            int room = Mathf.FloorToInt(smelter.m_maxFuel - smelter.GetFuel());
            int planned = Mathf.Min(Mathf.Min(room, item.m_stack), budget);
            int moved = 0;

            for (int i = 0; i < planned; i++)
            {
                if (!Move(smelter, source, item, 1))
                {
                    break;
                }

                moved++;
                smelter.m_nview.InvokeRPC("RPC_AddFuel");
            }

            if (moved > 0)
            {
                MoveLog.RecordStationFeed(container, item.m_shared.m_name, moved);
            }

            return moved;
        }

        private static int FeedOre(Smelter smelter, Container container, Inventory source, ItemDrop.ItemData item, string prefabName, int budget)
        {
            int room = smelter.m_maxOre - smelter.GetQueueSize();
            int planned = Mathf.Min(Mathf.Min(room, item.m_stack), budget);
            bool cheated = item.m_cheated;
            int moved = 0;

            for (int i = 0; i < planned; i++)
            {
                if (!Move(smelter, source, item, 1))
                {
                    break;
                }

                moved++;
                smelter.m_nview.InvokeRPC("RPC_AddOre", prefabName, cheated);
            }

            if (moved > 0)
            {
                MoveLog.RecordStationFeed(container, item.m_shared.m_name, moved);
            }

            return moved;
        }

        /// <summary>
        /// Se descuenta del cofre ANTES de mandar el RPC, y se revalida la propiedad del horno
        /// justo antes. Si algo fallara en el medio se pierde una unidad en vez de duplicarla:
        /// entre las dos, la duplicacion es peor para la integridad de la partida.
        /// </summary>
        private static bool Move(Smelter smelter, Inventory source, ItemDrop.ItemData item, int amount)
        {
            if (smelter.m_nview == null || !smelter.m_nview.IsValid() || !smelter.m_nview.IsOwner())
            {
                return false;
            }

            return source.RemoveItem(item, amount);
        }

        private static void PruneStale(float now)
        {
            if (NextCheck.Count < 64)
            {
                return;
            }

            PruneBuffer.Clear();
            foreach (KeyValuePair<Smelter, float> entry in NextCheck)
            {
                if (entry.Key == null || now > entry.Value + 300f)
                {
                    PruneBuffer.Add(entry.Key);
                }
            }

            foreach (Smelter smelter in PruneBuffer)
            {
                NextCheck.Remove(smelter);
            }

            PruneBuffer.Clear();
        }

        public static void Reset()
        {
            NextCheck.Clear();
            Buffer.Clear();
            DiagBuffer.Clear();
            ItemBuffer.Clear();
            _stationsInWindow = 0;
            _budgetWindowStart = 0f;
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class Smelter_UpdateSmelter_Patch
    {
        private static void Postfix(Smelter __instance)
        {
            try
            {
                StationFeeder.Tick(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al cargar el horno desde los cofres: {e}");
            }
        }
    }
}
