using System;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Core;
using QuickStash.Features;
using QuickStash.UI;

namespace QuickStash
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.valheimcrew.quickstash";
        public const string PluginName = "QuickStash";
        public const string PluginVersion = "1.3.4";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            PluginConfig.Bind(base.Config);

            PluginConfig.Enabled.SettingChanged += (_, __) => StashButton.ApplyConfig();
            PluginConfig.ShowButton.SettingChanged += (_, __) => StashButton.ApplyConfig();
            PluginConfig.ButtonLabel.SettingChanged += (_, __) => StashButton.ApplyConfig();
            PluginConfig.ButtonOffsetX.SettingChanged += (_, __) => StashButton.ApplyConfig();
            PluginConfig.ButtonOffsetY.SettingChanged += (_, __) => StashButton.ApplyConfig();

            _harmony = new Harmony(PluginGuid);
            ApplyPatches();

            WarnAboutConflicts();

            Log.LogInfo($"{PluginName} {PluginVersion} cargado.");
        }

        /// <summary>
        /// Se parchea por feature en vez de con un PatchAll global. Nueve de los parches apuntan
        /// a metodos privados del juego por nombre en string: si un parche de Valheim renombra
        /// uno solo, un PatchAll global tiraria y el mod entero no cargaria. Asi, un rename
        /// apaga una feature y el resto sigue andando, con el motivo en el log.
        /// </summary>
        private void ApplyPatches()
        {
            PatchGroup("registro de cofres",
                typeof(Container_Awake_Patch),
                typeof(Container_OnDestroyed_Patch));

            PatchGroup("ciclo de vida",
                typeof(Player_OnSpawned_Patch),
                typeof(Game_Logout_Patch));

            PatchGroup("guardado rapido",
                typeof(Container_RPC_StackResponse_Patch),
                typeof(InventoryGui_Awake_Patch));

            PatchGroup("favoritos",
                typeof(InventoryGrid_OnLeftDown_Patch),
                typeof(InventoryGrid_UpdateGui_Patch));

            PatchGroup("crafteo desde cofres",
                typeof(Player_HaveRequirementItems_Patch),
                typeof(InventoryGui_SetupRequirement_Patch),
                typeof(InventoryGui_DoCrafting_Patch));

            PatchGroup("construccion desde cofres",
                typeof(Player_HaveRequirements_Piece_Patch),
                typeof(Player_TryPlacePiece_Patch));

            PatchGroup("hornos automaticos",
                typeof(Smelter_UpdateSmelter_Patch));

        }

        private void PatchGroup(string feature, params Type[] patchTypes)
        {
            foreach (Type patchType in patchTypes)
            {
                try
                {
                    _harmony.PatchAll(patchType);
                }
                catch (Exception e)
                {
                    Log.LogError(
                        $"No se pudo enganchar '{feature}' ({patchType.Name}). Esa parte del mod " +
                        $"queda desactivada; probablemente el juego cambio en un parche. Detalle: {e.Message}");
                }
            }
        }

        private void Update()
        {
            if (!PluginConfig.Enabled.Value || Player.m_localPlayer == null)
            {
                return;
            }

            FavoriteStore.Tick();
            StashService.Tick();

            // Aislados: una excepcion aca abortaria el resto del Update —incluido el atajo de
            // guardado— y Unity la loguearia 60 veces por segundo. Los parches ya tienen su
            // propio try/catch; estos corren desde Update y necesitan el suyo.
            SafeTick(AnimalFeeder.Tick, "alimentacion de animales");
            SafeTick(AnimalDiagnostics.Tick, "diagnostico de animales");

            if (PluginConfig.Hotkey.Value.IsDown() && !IsTyping())
            {
                StashService.Run();
            }
        }

        private static void SafeTick(Action tick, string what)
        {
            try
            {
                tick();
            }
            catch (Exception e)
            {
                Log.LogError($"Fallo en {what}: {e}");
            }
        }

        /// <summary>
        /// Con un atajo de una sola tecla hay que asegurarse de que no se dispare mientras el
        /// jugador escribe: chat, consola, carteles y el comercio del mercader capturan texto.
        /// </summary>
        private static bool IsTyping()
        {
            if (Chat.instance != null && Chat.instance.HasFocus())
            {
                return true;
            }

            return Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible();
        }

        private void OnDestroy()
        {
            FavoriteStore.FlushIfDirty();
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Avisa si hay otro mod cargado que hace lo mismo. Con 14 jugadores basta que uno
        /// tenga un mod viejo de crafteo desde cofres para que los materiales se descuenten
        /// dos veces, y el sintoma es dificil de rastrear sin este aviso en el log.
        /// </summary>
        private static void WarnAboutConflicts()
        {
            string[] conflicting = Chainloader.PluginInfos.Keys
                .Where(guid =>
                    guid.IndexOf("craftfromcontainers", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    guid.IndexOf("quick_stack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    guid.IndexOf("quickstack", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            if (conflicting.Length > 0)
            {
                Log.LogWarning(
                    "Hay otros mods cargados que cubren lo mismo que QuickStash: " +
                    string.Join(", ", conflicting) +
                    ". Conviene dejar solo uno para evitar comportamientos duplicados.");
            }
        }
    }
}



