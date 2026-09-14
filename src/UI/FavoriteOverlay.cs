using System;
using System.Collections.Generic;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Features;
using UnityEngine;
using UnityEngine.UI;

namespace QuickStash.UI
{
    /// <summary>
    /// Dibuja un borde de color en las casillas favoritas del inventario.
    ///
    /// El sprite del borde se genera en memoria al vuelo, asi que el mod no necesita
    /// AssetBundles ni imagenes embebidas. Como InventoryGrid.UpdateGui corre en cada frame
    /// mientras la GUI esta abierta, el recorrido es O(items + casillas) y las referencias a
    /// los bordes quedan cacheadas por casilla en vez de buscarlas por nombre cada vez.
    /// </summary>
    internal static class FavoriteOverlay
    {
        private const string OverlayName = "QuickStashFavorite";
        private const int SpriteSize = 32;
        private const int BorderThickness = 3;

        private static Sprite _border;

        /// <summary>
        /// Si el dibujado falla una vez, se apaga para siempre. Este codigo cuelga de
        /// InventoryGrid.UpdateGui, que corre en cada frame: una excepcion se propaga al metodo
        /// vanilla y le deja la grilla del inventario congelada al jugador.
        /// </summary>
        private static bool _disabled;

        private static readonly Dictionary<InventoryElement, Image> Overlays =
            new Dictionary<InventoryElement, Image>();

        private static readonly HashSet<int> FavoritePositions = new HashSet<int>();

        private static int Pack(int x, int y) => (y << 8) | (x & 0xFF);

        public static void Refresh(InventoryGrid grid)
        {
            if (_disabled || !PluginConfig.Enabled.Value || grid == null || grid.m_elements == null)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || grid.m_inventory != player.GetInventory())
            {
                return;
            }

            // Una sola pasada por el inventario para saber que casillas tienen un item favorito.
            FavoritePositions.Clear();
            List<ItemDrop.ItemData> items = grid.m_inventory.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (FavoriteStore.IsFavoriteItem(item))
                {
                    FavoritePositions.Add(Pack(item.m_gridPos.x, item.m_gridPos.y));
                }
            }

            List<InventoryElement> elements = grid.m_elements;
            for (int i = 0; i < elements.Count; i++)
            {
                InventoryElement element = elements[i];
                if (element == null)
                {
                    continue;
                }

                Vector2i position = element.Position;
                bool favoriteSlot = FavoriteStore.IsFavoriteSlot(position.x, position.y);
                bool favoriteItem = FavoritePositions.Contains(Pack(position.x, position.y));

                if (!favoriteSlot && !favoriteItem)
                {
                    if (Overlays.TryGetValue(element, out Image existing) && existing != null && existing.enabled)
                    {
                        existing.enabled = false;
                    }

                    continue;
                }

                Image overlay = GetOrCreate(element);
                if (overlay == null)
                {
                    continue;
                }

                // La casilla marcada manda sobre el tipo de item, para que se vea que el
                // hueco esta reservado aunque el item que haya dentro no sea favorito.
                overlay.color = favoriteSlot ? PluginConfig.SlotColor : PluginConfig.FavColor;
                overlay.enabled = true;
            }
        }

        private static Image GetOrCreate(InventoryElement element)
        {
            bool found = Overlays.TryGetValue(element, out Image cached);
            if (found && cached != null)
            {
                return cached;
            }

            if (found)
            {
                Overlays.Remove(element);
                PruneDestroyed();
            }

            GameObject go = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(element.transform, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            Image image = go.GetComponent<Image>();
            image.sprite = BorderSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            Overlays[element] = image;
            return image;
        }

        public static void DisableAfterFailure(Exception e)
        {
            if (_disabled)
            {
                return;
            }

            _disabled = true;
            Plugin.Log.LogError(
                "Se desactivaron los bordes de favoritos porque el dibujado fallo. " +
                $"El resto del mod sigue funcionando. Detalle: {e}");
        }

        private static void PruneDestroyed()
        {
            if (Overlays.Count < 128)
            {
                return;
            }

            List<InventoryElement> dead = new List<InventoryElement>();
            foreach (KeyValuePair<InventoryElement, Image> pair in Overlays)
            {
                if (pair.Key == null)
                {
                    dead.Add(pair.Key);
                }
            }

            foreach (InventoryElement element in dead)
            {
                Overlays.Remove(element);
            }
        }

        /// <summary>Marco hueco de 32x32 en 9 slices, para que el grosor no se deforme al escalar.</summary>
        private static Sprite BorderSprite()
        {
            if (_border != null)
            {
                return _border;
            }

            Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32 transparent = new Color32(255, 255, 255, 0);
            Color32 solid = new Color32(255, 255, 255, 255);
            Color32[] pixels = new Color32[SpriteSize * SpriteSize];

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    bool onBorder = x < BorderThickness || y < BorderThickness ||
                                    x >= SpriteSize - BorderThickness || y >= SpriteSize - BorderThickness;
                    pixels[y * SpriteSize + x] = onBorder ? solid : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _border = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(BorderThickness, BorderThickness, BorderThickness, BorderThickness));
            _border.hideFlags = HideFlags.HideAndDontSave;

            return _border;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class InventoryGrid_UpdateGui_Patch
    {
        private static void Postfix(InventoryGrid __instance)
        {
            try
            {
                FavoriteOverlay.Refresh(__instance);
            }
            catch (Exception e)
            {
                FavoriteOverlay.DisableAfterFailure(e);
            }
        }
    }
}
