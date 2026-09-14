using System;
using HarmonyLib;

namespace QuickStash.Features
{
    /// <summary>
    /// Construir con el martillo usando el material de los cofres cercanos.
    ///
    /// El flujo vanilla en Player.UpdatePlacement es:
    ///
    ///     if (m_noPlacementCost || HaveRequirements(pieza, CanBuild))
    ///         if (TryPlacePiece(pieza))
    ///             ...
    ///             ConsumeResources(pieza.m_resources, 0);
    ///
    /// El material se trae en un PREFIX de TryPlacePiece, no en un postfix, y despues se
    /// revalida contra el inventario real. El motivo es que
    /// `Inventory.RemoveItem(string, int, ...)` —la que usa ConsumeResources— es **parcial y
    /// silenciosa**: descuenta lo que encuentra y termina, sin devolver nada ni avisar. Si el
    /// material no llegara a entrar al inventario, la pieza quedaria construida pagando solo
    /// una parte del costo.
    ///
    /// Igual que en el crafteo, no se toca Player.ConsumeResources.
    ///
    /// La pantalla de requisitos no necesita parche propio: Hud.SetupPieceInfo usa el mismo
    /// InventoryGui.SetupRequirement que el crafteo, y ese postfix ya contempla construccion.
    /// </summary>
    internal static class BuildFromContainers
    {
        /// <summary>Lo que el juego usa al construir: nivel de calidad 0 y una sola unidad.</summary>
        private const int BuildQualityLevel = 0;
        private const int BuildMultiplier = 1;

        public static void PullFor(Player player, Piece piece)
        {
            CraftFromContainers.PullMissing(
                player,
                piece.m_resources,
                BuildQualityLevel,
                itemQuality: -1,
                multiplier: BuildMultiplier,
                onlyOneIngredient: false);
        }

        /// <summary>
        /// El mismo recuento que hace Player.HaveRequirements con RequirementMode.CanBuild, pero
        /// mirando SOLO el inventario. Es la red de seguridad antes de dejar que el juego
        /// construya y consuma.
        /// </summary>
        public static bool CoveredByInventory(Player player, Piece piece)
        {
            Inventory inventory = player.GetInventory();

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem || requirement.m_amount <= 0)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                if (inventory.CountItems(name) < requirement.m_amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Replica el recuento de Player.HaveRequirements(Piece, RequirementMode) sumando los
        /// cofres. CanBuild exige la cantidad completa; CanAlmostBuild solo que exista al menos
        /// uno, que es lo que el menu de construccion usa para pintar la pieza en gris claro.
        /// </summary>
        public static bool HaveWithContainers(Player player, Piece piece, Player.RequirementMode mode)
        {
            Inventory inventory = player.GetInventory();

            // El espacio libre se cuenta UNA vez para toda la pieza y se va gastando. Si cada
            // requisito preguntara por su cuenta, los dos materiales de una pieza verian el
            // mismo casillero libre y la pieza se habilitaria aunque solo entre uno.
            // (Aproximacion: se asume un casillero por material; un material que necesite varios
            // stacks puede quedar sobreestimado, y de eso se encarga la revalidacion del prefix.)
            int freeSlots = inventory.GetEmptySlots();

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem || requirement.m_amount <= 0)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int inInventory = inventory.CountItems(name);
                int available = inInventory;

                if (inInventory > 0 || freeSlots > 0)
                {
                    available += CraftFromContainers.InContainers(player, name, -1);

                    if (inInventory == 0)
                    {
                        freeSlots--;
                    }
                }

                int needed = mode == Player.RequirementMode.CanAlmostBuild ? 1 : requirement.m_amount;
                if (available < needed)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Habilita la pieza cuando el material falta en el inventario pero esta en los cofres.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    internal static class Player_HaveRequirements_Piece_Patch
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            // Corre muy seguido con el martillo en la mano, para pintar el fantasma y los iconos.
            if (__result || piece == null ||
                mode == Player.RequirementMode.IsKnown ||
                !CraftFromContainers.BuildActive(__instance))
            {
                return;
            }

            try
            {
                // Las condiciones previas del vanilla (estacion, DLC) ya decidieron antes de
                // llegar a los materiales: si fallaron, __result es false por un motivo que los
                // cofres no arreglan.
                if (!HasStationAndDlc(__instance, piece, mode))
                {
                    return;
                }

                __result = BuildFromContainers.HaveWithContainers(__instance, piece, mode);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al evaluar requisitos de construccion con cofres: {e}");
            }
        }

        /// <summary>
        /// Mismas condiciones no-materiales que chequea Player.HaveRequirements antes de los
        /// recursos. Ojo que el vanilla usa una regla distinta segun el modo: CanAlmostBuild
        /// solo pide conocer la estacion, CanBuild pide tenerla en rango.
        /// </summary>
        private static bool HasStationAndDlc(Player player, Piece piece, Player.RequirementMode mode)
        {
            if (piece.m_craftingStation != null)
            {
                bool ok = mode == Player.RequirementMode.CanAlmostBuild
                    ? player.m_knownStations.ContainsKey(piece.m_craftingStation.m_name)
                    : CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, player.transform.position)
                      || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);

                if (!ok)
                {
                    return false;
                }
            }

            if (piece.m_dlc.Length <= 0)
            {
                return true;
            }

            return DLCMan.instance != null && DLCMan.instance.IsDLCInstalled(piece.m_dlc);
        }
    }

    /// <summary>
    /// Trae el material de los cofres antes de que el juego coloque la pieza, y aborta si aun
    /// asi no alcanza. Ver el comentario de la clase: el consumo vanilla es silencioso, asi que
    /// esta revalidacion es lo unico que impide construir pagando de menos.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class Player_TryPlacePiece_Patch
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (piece == null || !CraftFromContainers.BuildActive(__instance))
            {
                return true;
            }

            try
            {
                // Si la construccion es gratis por global key, el vanilla no consume nada: sin
                // este chequeo se vaciarian los cofres hacia el inventario para nada.
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
                {
                    return true;
                }

                // El fantasma del frame anterior ya sabe si la posicion sirve. Esto evita sacar
                // material en colocaciones que el juego va a rechazar igual.
                if (__instance.m_placementStatus != Player.PlacementStatus.Valid)
                {
                    return true;
                }

                BuildFromContainers.PullFor(__instance, piece);

                if (!BuildFromContainers.CoveredByInventory(__instance, piece))
                {
                    // Se devuelve false para que el juego no coloque ni consuma. Es el unico
                    // prefix del mod que corta el original, y solo en el camino de fallo: el
                    // material que si se trajo queda en el inventario, no se pierde nada.
                    __instance.Message(MessageHud.MessageType.Center, "$msg_missingrequirement");
                    __result = false;
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al traer material de los cofres para construir: {e}");
            }

            return true;
        }
    }
}
