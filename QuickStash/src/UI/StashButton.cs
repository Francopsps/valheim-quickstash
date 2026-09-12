using System;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Features;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuickStash.UI
{
    /// <summary>
    /// Boton de guardado dentro del panel del inventario.
    ///
    /// Se clona el boton vanilla de apilar (m_stackAllButton) en vez de construir uno nuevo:
    /// asi hereda fuente, sprites, sonidos y estados de hover del juego, y no hay que cargar
    /// ningun asset propio. Si el boton vanilla no existiera tras un parche del juego, el mod
    /// avisa por log y sigue funcionando con el atajo de teclado.
    /// </summary>
    internal static class StashButton
    {
        private static GameObject _root;
        private static RectTransform _rect;
        private static TMP_Text _label;

        public static void Build(InventoryGui gui)
        {
            try
            {
                if (gui == null || gui.m_player == null)
                {
                    return;
                }

                if (gui.m_stackAllButton == null)
                {
                    Plugin.Log.LogWarning(
                        "No se encontro el boton vanilla de apilar, no se puede crear el boton de guardado. " +
                        "Usa el atajo de teclado configurable en su lugar.");
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(gui.m_stackAllButton.gameObject, gui.m_player);
                clone.name = "QuickStashButton";

                // El componente Localize reescribe los textos del subarbol al cambiar de idioma.
                foreach (Localize localize in clone.GetComponentsInChildren<Localize>(true))
                {
                    UnityEngine.Object.Destroy(localize);
                }

                Button button = clone.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(StashService.Run);
                }

                _root = clone;
                _rect = clone.GetComponent<RectTransform>();
                _label = clone.GetComponentInChildren<TMP_Text>(true);

                if (_rect != null)
                {
                    _rect.anchorMin = new Vector2(1f, 1f);
                    _rect.anchorMax = new Vector2(1f, 1f);
                    _rect.pivot = new Vector2(1f, 1f);
                }

                ApplyConfig();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"No se pudo crear el boton de guardado: {e}");
            }
        }

        public static void ApplyConfig()
        {
            if (_root == null)
            {
                return;
            }

            _root.SetActive(PluginConfig.Enabled.Value && PluginConfig.ShowButton.Value);

            if (_label != null)
            {
                _label.text = PluginConfig.ButtonLabel.Value;
            }

            if (_rect != null)
            {
                // Por defecto queda arriba a la derecha del panel del inventario. La posicion
                // exacta depende de la resolucion y de otros mods de UI, por eso es ajustable.
                _rect.anchoredPosition = new Vector2(
                    -12f + PluginConfig.ButtonOffsetX.Value,
                    36f + PluginConfig.ButtonOffsetY.Value);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    internal static class InventoryGui_Awake_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            StashButton.Build(__instance);
        }
    }
}
