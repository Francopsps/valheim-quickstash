using HarmonyLib;
using QuickStash.Features;

namespace QuickStash.Core
{
    /// <summary>Carga los favoritos al entrar con un personaje y los baja a disco al salir.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class Player_OnSpawned_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                FavoriteStore.LoadForCurrentProfile();
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    internal static class Game_Logout_Patch
    {
        private static void Prefix()
        {
            FavoriteStore.FlushIfDirty();
            ContainerRegistry.Clear();
            ContainerIndex.Invalidate();

            // Sin esto una accion a medio terminar deja _running en true y el proximo mundo
            // arranca con el guardado rapido bloqueado hasta que venza el timeout.
            StashService.Reset();
            StationFeeder.Reset();
            AnimalFeeder.Reset();
            AnimalDiagnostics.Reset();
        }
    }
}
