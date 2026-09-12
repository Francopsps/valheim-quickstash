using System;
using System.Collections.Generic;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Core;
using UnityEngine;

namespace QuickStash.Features
{
    /// <summary>
    /// Guardado rapido: manda a los cofres cercanos los items que esos cofres ya contienen,
    /// saltando favoritos, equipados y la barra rapida.
    ///
    /// Para tocar un cofre ajeno se usa el mismo protocolo que el boton vanilla de apilar
    /// (RPC_RequestStack): se le pide permiso al dueno, que valida que nadie lo tenga abierto
    /// y que tengamos acceso, y recien ahi nos transfiere la propiedad. Es mas seguro que
    /// arrebatar la propiedad con ClaimOwnership.
    ///
    /// Ojo con la reentrada: si el ZDO ya es nuestro, ZRoutedRpc despacha el RPC en la misma
    /// pila (ZRoutedRpc.InvokeRoutedRPC llama a HandleRoutedRPC directo cuando el destino es
    /// uno mismo), asi que la respuesta llega ANTES de que InvokeRPC retorne. Por eso el
    /// estado de la accion se arma antes del bucle y el informe se posterga con _dispatching.
    /// </summary>
    internal static class StashService
    {
        private const float ResponseTimeoutSeconds = 5f;

        /// <summary>
        /// Cuanto se recuerda una peticion propia. Es largo a proposito: mientras la peticion
        /// siga registrada, su respuesta se descarta en silencio. Si la olvidaramos, la
        /// respuesta caeria en Container.RPC_StackResponse vanilla, que apila con
        /// Inventory.StackAll y NO conoce favoritos, barra rapida ni exclusiones.
        /// </summary>
        private const float RequestGraceSeconds = 60f;

        private const float CooldownSeconds = 0.5f;

        private struct Request
        {
            public int ActionId;
            public float ExpiresAt;
        }

        private static readonly List<Container> Buffer = new List<Container>(64);
        private static readonly List<ItemDrop.ItemData> ItemBuffer = new List<ItemDrop.ItemData>(64);
        private static readonly Dictionary<Container, Request> Requested = new Dictionary<Container, Request>();
        private static readonly List<Container> PruneBuffer = new List<Container>(16);
        private static readonly Dictionary<string, int> MovedBuffer = new Dictionary<string, int>(16);

        private static int _actionId;
        private static int _awaiting;
        private static bool _dispatching;
        private static bool _reported;
        private static bool _running;
        private static float _deadline;
        private static float _nextAllowedAt;

        private static int _movedTotal;
        private static int _containersTouched;
        private static int _denied;
        private static float _startedAt;
        private static float _syncMs;

        private static ItemDrop.ItemData _dragged;

        public static void Run()
        {
            if (!PluginConfig.Enabled.Value)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (_running || now < _nextAllowedAt)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting())
            {
                return;
            }

            _nextAllowedAt = now + CooldownSeconds;
            _actionId++;
            _movedTotal = 0;
            _containersTouched = 0;
            _denied = 0;
            _awaiting = 0;
            _reported = false;
            _startedAt = now;
            _running = true;
            _deadline = now + ResponseTimeoutSeconds;
            _dragged = InventoryGui.instance != null ? InventoryGui.instance.m_dragItem : null;

            _dispatching = true;
            try
            {
                long playerId = ContainerAccess.LocalPlayerId();
                Container open = InventoryGui.instance != null ? InventoryGui.instance.m_currentContainer : null;

                if (PluginConfig.IncludeOpenContainer.Value && open != null)
                {
                    int moved = StackInto(open, player);
                    if (moved > 0)
                    {
                        _movedTotal += moved;
                        _containersTouched++;
                    }
                }

                ContainerRegistry.Query(
                    player.transform.position,
                    PluginConfig.Range.Value,
                    PluginConfig.MaxContainersPerAction.Value,
                    playerId,
                    Buffer);

                foreach (Container container in Buffer)
                {
                    if (container == open || !HasAnythingFor(container, player))
                    {
                        continue;
                    }

                    // El contador sube ANTES del RPC porque la respuesta puede llegar dentro
                    // de la propia llamada cuando el ZDO ya es nuestro.
                    _awaiting++;
                    Requested[container] = new Request
                    {
                        ActionId = _actionId,
                        ExpiresAt = now + RequestGraceSeconds
                    };

                    container.m_nview.InvokeRPC("RPC_RequestStack", playerId);
                }

                _syncMs = (Time.realtimeSinceStartup - now) * 1000f;
            }
            finally
            {
                _dispatching = false;
            }

            if (_awaiting == 0)
            {
                Report();
            }
        }

        /// <summary>
        /// Devuelve true si la respuesta corresponde a una peticion nuestra, en cuyo caso el
        /// parche salta la implementacion vanilla. Toda peticion propia se consume aca, incluso
        /// si llego tarde: dejarla pasar al vanilla apilaria ignorando los favoritos.
        /// </summary>
        public static bool TryHandleResponse(Container container, bool granted)
        {
            if (container == null || !Requested.TryGetValue(container, out Request request))
            {
                return false;
            }

            Requested.Remove(container);

            if (request.ActionId != _actionId)
            {
                return true;
            }

            if (granted)
            {
                Player player = Player.m_localPlayer;
                if (player != null && container.m_nview != null && container.m_nview.IsValid())
                {
                    // El dueno ya nos transfirio el ZDO al conceder, pero lo confirmamos igual:
                    // Container solo guarda si somos duenos.
                    container.m_nview.ClaimOwnership();

                    int moved = StackInto(container, player);
                    if (moved > 0)
                    {
                        _movedTotal += moved;
                        _containersTouched++;
                    }
                }
            }
            else
            {
                _denied++;
            }

            if (_awaiting > 0)
            {
                _awaiting--;
            }

            if (!_dispatching && _awaiting == 0)
            {
                Report();
            }

            return true;
        }

        /// <summary>Red de seguridad por si alguna respuesta nunca llega, mas poda de peticiones viejas.</summary>
        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;

            if (_running && now >= _deadline)
            {
                _awaiting = 0;
                Report();
            }

            PruneExpiredRequests(now);
        }

        public static void Reset()
        {
            Requested.Clear();
            Buffer.Clear();
            ItemBuffer.Clear();
            _awaiting = 0;
            _dispatching = false;
            _running = false;
            _reported = true;
            _dragged = null;
        }

        private static void PruneExpiredRequests(float now)
        {
            if (Requested.Count == 0)
            {
                return;
            }

            PruneBuffer.Clear();
            foreach (KeyValuePair<Container, Request> pair in Requested)
            {
                if (pair.Key == null || now >= pair.Value.ExpiresAt)
                {
                    PruneBuffer.Add(pair.Key);
                }
            }

            foreach (Container container in PruneBuffer)
            {
                Requested.Remove(container);
            }

            PruneBuffer.Clear();
        }

        /// <summary>
        /// Filtro previo al RPC: el inventario de los cofres remotos ya esta replicado en
        /// local (Container.CheckForChanges hace Load cada segundo), asi que preguntarle es
        /// gratis. Sin esto se pediria la propiedad de hasta 32 cofres por pulsacion, y del
        /// lado del dueno cada peticion fuerza un reenvio del ZDO completo.
        /// </summary>
        private static bool HasAnythingFor(Container container, Player player)
        {
            Inventory target = container.GetInventory();
            Inventory source = player.GetInventory();
            if (target == null || source == null)
            {
                return false;
            }

            List<ItemDrop.ItemData> items = source.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (CanStash(item, player) && target.ContainsItemByName(item.m_shared.m_name))
                {
                    return true;
                }
            }

            return false;
        }

        private static int StackInto(Container container, Player player)
        {
            ZNetView nview = container.m_nview;

            // Sin propiedad del ZDO, Container.OnContainerChanged no llama a Save(): el
            // movimiento queda solo en memoria y el siguiente Load() lo revierte, con los
            // items ya descontados del inventario del jugador. Es perdida, no duplicacion.
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return 0;
            }

            Inventory target = container.GetInventory();
            Inventory source = player.GetInventory();
            if (target == null || source == null)
            {
                return 0;
            }

            int moved = 0;

            // Desconectar el callback evita que el cofre se serialice una vez por item movido.
            // Al final se dispara un solo Changed(), que produce un unico Save().
            Action previousHandler = target.m_onChanged;
            target.m_onChanged = null;

            try
            {
                ItemBuffer.Clear();
                ItemBuffer.AddRange(source.GetAllItems());
                MovedBuffer.Clear();

                foreach (ItemDrop.ItemData item in ItemBuffer)
                {
                    if (!CanStash(item, player))
                    {
                        continue;
                    }

                    // Misma regla que el apilado vanilla: solo a cofres que ya tienen ese item.
                    if (!target.ContainsItemByName(item.m_shared.m_name))
                    {
                        continue;
                    }

                    // Se cuenta a mano en vez de con el delta de CountItems porque ese metodo
                    // filtra por Game.m_worldLevel: con nivel de mundo > 0, mover items viejos
                    // daria delta cero y el cofre no llegaria a guardar nunca.
                    int stackBefore = item.m_stack;
                    int justMoved;

                    if (target.AddItem(item))
                    {
                        source.RemoveItem(item);
                        justMoved = stackBefore;
                    }
                    else
                    {
                        // AddItem pudo fundir parte del stack antes de quedarse sin casilla.
                        justMoved = stackBefore - item.m_stack;
                    }

                    if (justMoved > 0)
                    {
                        moved += justMoved;
                        string label = MoveLog.Localize(item.m_shared.m_name);
                        MovedBuffer.TryGetValue(label, out int already);
                        MovedBuffer[label] = already + justMoved;
                    }
                }
            }
            finally
            {
                ItemBuffer.Clear();
                target.m_onChanged = previousHandler;
            }

            if (moved > 0)
            {
                target.Changed();
                source.Changed();
                MoveLog.Record(intoContainer: true, container, MovedBuffer);
            }

            MovedBuffer.Clear();
            return moved;
        }

        private static bool CanStash(ItemDrop.ItemData item, Player player)
        {
            if (item?.m_shared == null || item == _dragged)
            {
                return false;
            }

            if (player.IsItemEquiped(item))
            {
                return false;
            }

            if (PluginConfig.ProtectHotbar.Value && item.m_gridPos.y == 0)
            {
                return false;
            }

            if (FavoriteStore.IsProtected(item))
            {
                return false;
            }

            return !PluginConfig.IsExcluded(item);
        }

        private static void Report()
        {
            if (_reported)
            {
                return;
            }

            _reported = true;
            _running = false;
            _dragged = null;

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            if (PluginConfig.DebugTiming.Value)
            {
                float totalMs = (Time.realtimeSinceStartup - _startedAt) * 1000f;
                Plugin.Log.LogInfo(
                    $"[stash] {_movedTotal} items -> {_containersTouched} cofres | " +
                    $"sincrono {_syncMs:F2} ms | total {totalMs:F0} ms | rechazados {_denied} | " +
                    $"registro {ContainerRegistry.Count} cofres | peticiones vivas {Requested.Count}");
            }

            if (_movedTotal > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    $"Guardado: {_movedTotal} en {_containersTouched} cofre(s)");

                if (InventoryGui.instance != null)
                {
                    InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
                }
            }
            else if (_denied > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    $"Sin guardar: {_denied} cofre(s) en uso por otro jugador");
            }
            else
            {
                player.Message(MessageHud.MessageType.Center, "Nada que guardar cerca");
            }
        }
    }

    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    internal static class Container_RPC_StackResponse_Patch
    {
        private static bool Prefix(Container __instance, bool granted)
        {
            try
            {
                return !StashService.TryHandleResponse(__instance, granted);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al procesar la respuesta de apilado: {e}");

                // Ante un fallo propio se traga la respuesta en vez de dejarla pasar: el
                // apilado vanilla ignoraria los favoritos del jugador.
                return false;
            }
        }
    }
}
