namespace QuickStash.Core
{
    /// <summary>
    /// Reglas para decidir si el jugador local puede tocar un cofre.
    /// Replica exactamente lo que valida el propio juego en Container.Interact
    /// y Container.RPC_RequestStack, para no ser ni mas permisivo ni mas restrictivo
    /// que el vanilla.
    /// </summary>
    internal static class ContainerAccess
    {
        public static bool CanUse(Container container, long playerId)
        {
            if (container == null)
            {
                return false;
            }

            ZNetView nview = container.m_nview;
            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            // Critico con muchos jugadores: nunca escribir en un cofre que otro tiene abierto.
            if (IsInUse(container, nview))
            {
                return false;
            }

            // Cofres privados y de grupo.
            if (!container.CheckAccess(playerId))
            {
                return false;
            }

            // Wards, con la misma condicion que usa el juego.
            if (container.m_checkGuardStone &&
                !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// m_inUse solo es fiable en el cliente que es dueno del cofre. Para cofres remotos
        /// el estado viaja en el ZDO, que es de donde lo lee Container.UpdateUseVisual.
        /// </summary>
        public static bool IsInUse(Container container, ZNetView nview)
        {
            if (nview.IsOwner())
            {
                return container.m_inUse;
            }

            ZDO zdo = nview.GetZDO();
            return zdo != null && zdo.GetInt(ZDOVars.s_inUse) == 1;
        }

        public static long LocalPlayerId()
        {
            if (Game.instance == null)
            {
                return 0L;
            }

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            return profile == null ? 0L : profile.GetPlayerID();
        }
    }
}
