using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace QuickStash.Config
{
    /// <summary>
    /// Toda la configuracion del mod, en BepInEx\config\com.valheimcrew.quickstash.cfg.
    ///
    /// Editado a mano necesita reinicio: BepInEx 5 no vigila el archivo. Con el mod
    /// ConfigurationManager instalado aparte (F1) los cambios entran en caliente, por eso los
    /// valores se releen en cada uso y hay SettingChanged donde hace falta reaccionar.
    /// </summary>
    internal static class PluginConfig
    {
        private const string General = "1 - General";
        private const string Stash = "2 - Guardado rapido";
        private const string Interfaz = "3 - Interfaz";
        private const string Favoritos = "4 - Favoritos";
        private const string Crafteo = "5 - Crafteo desde cofres";
        private const string Rendimiento = "6 - Rendimiento";

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugTiming;
        public static ConfigEntry<bool> LogMoves;

        public static ConfigEntry<float> Range;
        public static ConfigEntry<KeyboardShortcut> Hotkey;
        public static ConfigEntry<bool> ProtectHotbar;
        public static ConfigEntry<bool> IncludeOpenContainer;
        public static ConfigEntry<int> MaxContainersPerAction;
        public static ConfigEntry<string> ExcludedItems;

        public static ConfigEntry<bool> ShowButton;
        public static ConfigEntry<string> ButtonLabel;
        public static ConfigEntry<float> ButtonOffsetX;
        public static ConfigEntry<float> ButtonOffsetY;

        public static ConfigEntry<KeyCode> FavoriteModifier;
        public static ConfigEntry<KeyCode> SlotModifier;
        public static ConfigEntry<string> FavoriteColor;
        public static ConfigEntry<string> SlotFavoriteColor;

        public static ConfigEntry<bool> CraftFromContainers;
        public static ConfigEntry<float> CraftRange;
        public static ConfigEntry<bool> ShowContainerTotals;

        public static ConfigEntry<int> CacheMs;

        private static HashSet<string> _excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Color _favColor = new Color(1f, 0.82f, 0.16f);
        private static Color _slotColor = new Color(0.30f, 0.80f, 1f);

        public static Color FavColor => _favColor;
        public static Color SlotColor => _slotColor;

        public static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind(General, "Activado", true,
                "Activa o desactiva el mod completo sin necesidad de sacar el DLL.");
            DebugTiming = cfg.Bind(General, "LogDeRendimiento", false,
                "Escribe en el log los milisegundos y la cantidad de cofres de cada accion. Util para medir en el servidor.");
            LogMoves = cfg.Bind(General, "LogDeMovimientos", true,
                "Anota en BepInEx/LogOutput.log cada item que el mod mueve, con cantidad, cofre, posicion y hora. Dejalo activado: es el unico rastro para investigar si alguna vez falta algo.");

            Range = cfg.Bind(Stash, "Rango", 30f,
                new ConfigDescription("Distancia en metros para buscar cofres al guardar.",
                    new AcceptableValueRange<float>(2f, 50f)));
            Hotkey = cfg.Bind(Stash, "Atajo", new KeyboardShortcut(KeyCode.N),
                "Tecla para guardar sin abrir el inventario. No se dispara mientras escribis en el chat, la consola o un cartel.");
            ProtectHotbar = cfg.Bind(Stash, "ProtegerBarraRapida", true,
                "No guarda los items de la primera fila del inventario (la barra rapida).");
            IncludeOpenContainer = cfg.Bind(Stash, "IncluirCofreAbierto", true,
                "Si hay un cofre abierto, se usa primero.");
            MaxContainersPerAction = cfg.Bind(Stash, "MaxCofresPorAccion", 32,
                new ConfigDescription("Tope de cofres que se procesan en una pulsacion. Protege al servidor con muchos jugadores.",
                    new AcceptableValueRange<int>(1, 64)));
            ExcludedItems = cfg.Bind(Stash, "ItemsExcluidos", "",
                "Nombres de prefab separados por coma que nunca se guardan. Ejemplo: Wood,Stone");

            ShowButton = cfg.Bind(Interfaz, "MostrarBoton", true,
                "Muestra el boton de guardado en el panel del inventario.");
            ButtonLabel = cfg.Bind(Interfaz, "TextoBoton", "Guardar",
                "Texto del boton.");
            ButtonOffsetX = cfg.Bind(Interfaz, "PosicionX", 0f,
                new ConfigDescription("Corrimiento horizontal del boton respecto a su posicion por defecto.",
                    new AcceptableValueRange<float>(-600f, 600f)));
            ButtonOffsetY = cfg.Bind(Interfaz, "PosicionY", 0f,
                new ConfigDescription("Corrimiento vertical del boton respecto a su posicion por defecto.",
                    new AcceptableValueRange<float>(-600f, 600f)));

            FavoriteModifier = cfg.Bind(Favoritos, "Modificador", KeyCode.LeftAlt,
                "Tecla que, junto con clic izquierdo, marca un item como favorito.");
            SlotModifier = cfg.Bind(Favoritos, "ModificadorCasilla", KeyCode.LeftShift,
                "Junto con el modificador anterior, marca la casilla en vez del tipo de item.");
            FavoriteColor = cfg.Bind(Favoritos, "ColorItem", "#FFD129",
                "Color del borde para items favoritos, en hexadecimal.");
            SlotFavoriteColor = cfg.Bind(Favoritos, "ColorCasilla", "#4DCCFF",
                "Color del borde para casillas favoritas, en hexadecimal.");

            CraftFromContainers = cfg.Bind(Crafteo, "Activado", true,
                "Permite craftear y mejorar usando materiales de los cofres cercanos. Solo actua estando en una estacion (forja, mesa de trabajo, etc).");
            CraftRange = cfg.Bind(Crafteo, "Rango", 30f,
                new ConfigDescription("Distancia en metros para buscar materiales al craftear.",
                    new AcceptableValueRange<float>(2f, 50f)));
            ShowContainerTotals = cfg.Bind(Crafteo, "MostrarTotales", true,
                "Muestra en la receta el total disponible incluyendo los cofres.");

            CacheMs = cfg.Bind(Rendimiento, "CacheMs", 500,
                new ConfigDescription("Milisegundos que se reutiliza la busqueda de cofres. Subirlo baja el uso de CPU. El minimo es 100: en cero el indice se reconstruiria varias veces por frame con el panel de crafteo abierto.",
                    new AcceptableValueRange<int>(100, 5000)));

            FavoriteModifier.SettingChanged += (_, __) => SanitizeModifiers();
            SlotModifier.SettingChanged += (_, __) => SanitizeModifiers();
            SanitizeModifiers();

            ExcludedItems.SettingChanged += (_, __) => RebuildExcluded();
            FavoriteColor.SettingChanged += (_, __) => RebuildColors();
            SlotFavoriteColor.SettingChanged += (_, __) => RebuildColors();
            RebuildExcluded();
            RebuildColors();
        }

        /// <summary>
        /// Un boton del mouse como modificador convierte todo clic izquierdo en un toggle de
        /// favorito y deja el inventario inutilizable, sin ningun mensaje que lo explique.
        /// </summary>
        private static void SanitizeModifiers()
        {
            Reject(FavoriteModifier, KeyCode.LeftAlt);
            Reject(SlotModifier, KeyCode.LeftShift);
        }

        private static void Reject(ConfigEntry<KeyCode> entry, KeyCode fallback)
        {
            if (entry.Value >= KeyCode.Mouse0 && entry.Value <= KeyCode.Mouse6)
            {
                Plugin.Log.LogWarning(
                    $"'{entry.Definition.Key}' no puede ser un boton del mouse ({entry.Value}): " +
                    $"romperia el arrastre de items. Se usa {fallback} en su lugar.");
                entry.Value = fallback;
            }
        }

        private static void RebuildExcluded()
        {
            _excluded = new HashSet<string>(
                (ExcludedItems.Value ?? string.Empty)
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void RebuildColors()
        {
            _favColor = ParseColor(FavoriteColor.Value, new Color(1f, 0.82f, 0.16f));
            _slotColor = ParseColor(SlotFavoriteColor.Value, new Color(0.30f, 0.80f, 1f));
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out Color parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static bool IsExcluded(ItemDrop.ItemData item)
        {
            if (_excluded.Count == 0 || item == null)
            {
                return false;
            }

            if (item.m_dropPrefab != null && _excluded.Contains(item.m_dropPrefab.name))
            {
                return true;
            }

            return item.m_shared != null && _excluded.Contains(item.m_shared.m_name);
        }
    }
}
