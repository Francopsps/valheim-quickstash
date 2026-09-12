using System;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Features;
using UnityEngine;

namespace QuickStash.UI
{
    /// <summary>
    /// Alt+clic sobre el inventario marca favoritos.
    ///
    /// Se intercepta OnLeftDown, que es donde el juego decide agarrar o mover el item, asi
    /// que al marcar un favorito el clic no arrastra nada. Solo aplica al inventario del
    /// jugador: sobre un cofre el clic funciona como siempre.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "OnLeftDown")]
    internal static class InventoryGrid_OnLeftDown_Patch
    {
        private static bool Prefix(InventoryGrid __instance, UIInputHandler clickHandler)
        {
            if (!PluginConfig.Enabled.Value || clickHandler == null)
            {
                return true;
            }

            if (!ZInput.GetKey(PluginConfig.FavoriteModifier.Value))
            {
                return true;
            }

            Player player = Player.m_localPlayer;
            if (player == null || __instance.m_inventory != player.GetInventory())
            {
                return true;
            }

            try
            {
                Vector2i position = __instance.GetButtonPos(clickHandler.gameObject);

                // GetButtonPos devuelve (-1,-1) si el objeto clickeado no esta en la grilla.
                // Sin este guard se marcaria una casilla inexistente y se persistiria basura.
                if (position.x < 0 || position.y < 0)
                {
                    return true;
                }

                ItemDrop.ItemData item = __instance.m_inventory.GetItemAt(position.x, position.y);
                bool slotMode = item == null || ZInput.GetKey(PluginConfig.SlotModifier.Value);

                if (slotMode)
                {
                    bool added = FavoriteStore.ToggleSlot(position.x, position.y);
                    player.Message(MessageHud.MessageType.TopLeft,
                        added ? "Casilla protegida" : "Casilla desprotegida");
                }
                else
                {
                    bool added = FavoriteStore.ToggleItem(item);
                    string name = Localization.instance.Localize(item.m_shared.m_name);
                    player.Message(MessageHud.MessageType.TopLeft,
                        (added ? "Favorito: " : "Ya no es favorito: ") + name);
                }

                return false;
            }
            catch (Exception e)
            {
                // Devolver true deja que el clic haga lo de siempre en vez de quedar muerto.
                Plugin.Log.LogError($"Fallo al marcar favorito: {e}");
                return true;
            }
        }
    }
}
