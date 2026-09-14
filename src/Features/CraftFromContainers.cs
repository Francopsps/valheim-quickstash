using System;
using System.Collections.Generic;
using HarmonyLib;
using QuickStash.Config;
using QuickStash.Core;
using TMPro;
using UnityEngine;

namespace QuickStash.Features
{
    /// <summary>
    /// Permite craftear y mejorar usando el material de los cofres cercanos.
    ///
    /// Solo actua estando en una estacion (forja, mesa de trabajo, etc): fuera de una
    /// estacion todos los parches salen en la primera linea, asi que el crafteo a mano y
    /// el modo construccion quedan exactamente como el vanilla y sin costo de CPU.
    ///
    /// Son tres parches y ninguno toca Player.ConsumeResources, que es el metodo que suelen
    /// reemplazar otros mods de este tipo. En vez de eso, el material que falta se trae al
    /// inventario justo antes de que el juego craftee, de modo que el resto del flujo vanilla
    /// (requisitos, calidades, receta de un solo ingrediente) funciona sin modificaciones.
    /// </summary>
    internal static class CraftFromContainers
    {
        public static readonly Color AvailableColor = new Color(0.55f, 0.85f, 1f);

        public static bool Active(Player player)
        {
            return PluginConfig.Enabled.Value
                   && PluginConfig.CraftFromContainers.Value
                   && player != null
                   && player.GetCurrentCraftingStation() != null;
        }

        /// <summary>
        /// Player.InPlaceMode() es "tiene el martillo equipado" (m_buildPieces != null), NO
        /// "esta construyendo": con el martillo en la mano y la forja abierta da true igual. Lo
        /// que de verdad separa los dos modos es que panel esta en pantalla.
        /// </summary>
        public static bool IsBuilding(Player player)
        {
            return player != null && player.InPlaceMode() && !InventoryGui.IsVisible();
        }

        /// <summary>Construir con el martillo usando material de los cofres cercanos.</summary>
        public static bool BuildActive(Player player)
        {
            return PluginConfig.Enabled.Value
                   && PluginConfig.BuildFromContainers.Value
                   && IsBuilding(player);
        }

        /// <summary>
        /// Crafteo y construccion tienen rangos distintos y pueden estar los dos habilitados a
        /// la vez (martillo equipado + parado en la estacion), asi que el rango lo decide el
        /// modo en pantalla. Elegirlo aca evita arrastrar un parametro por toda la cadena.
        /// </summary>
        private static float ActiveRange(Player player)
        {
            return IsBuilding(player) ? PluginConfig.BuildRange.Value : PluginConfig.CraftRange.Value;
        }

        private static void Refresh(Player player)
        {
            ContainerIndex.EnsureFresh(
                player.transform.position,
                ActiveRange(player),
                ContainerAccess.LocalPlayerId());
        }

        /// <summary>Cuanto hay de ese material en los cofres cercanos. quality &lt; 0 suma todas las calidades.</summary>
        public static int InContainers(Player player, string name, int quality)
        {
            Refresh(player);
            return ContainerIndex.Count(name, quality);
        }

        /// <summary>
        /// El material del cofre solo sirve si hay donde ponerlo: o queda una casilla libre, o
        /// el jugador ya tiene un stack de eso al que sumarle. Sin esto la receta se mostraria
        /// disponible y el crafteo fallaria despues con el mensaje de inventario lleno.
        /// </summary>
        public static bool CanReceive(Inventory inventory, int alreadyInInventory)
        {
            return alreadyInInventory > 0 || inventory.HaveEmptySlot();
        }

        /// <summary>Material de los cofres utilizable de verdad, contemplando el espacio disponible.</summary>
        public static int CountAvailable(Player player, string name, int quality)
        {
            Inventory inventory = player.GetInventory();
            if (!CanReceive(inventory, inventory.CountItems(name)))
            {
                return 0;
            }

            return InContainers(player, name, quality);
        }

        public static bool IsRelevant(Piece.Requirement requirement, CraftingStation station)
        {
            if (requirement.m_resItem == null)
            {
                return false;
            }

            if (station != null && station.m_upgrader != requirement.m_upgraderResource)
            {
                return false;
            }

            return station != null || !requirement.m_upgraderResource;
        }

        /// <summary>
        /// Replica el barrido de Player.GetFirstRequiredItem: el juego exige que UNA sola
        /// calidad cubra el requisito, no la suma de todas. Recipe.GetAmount desreferencia sin
        /// chequear lo que devuelve GetFirstRequiredItem, asi que si esto da false y el
        /// crafteo sigue, el juego tira NullReferenceException.
        /// </summary>
        public static bool CoveredInSingleQuality(Inventory inventory, Piece.Requirement requirement, int need)
        {
            string name = requirement.m_resItem.m_itemData.m_shared.m_name;
            int maxQuality = requirement.m_resItem.m_itemData.m_shared.m_maxQuality;

            for (int quality = 0; quality <= maxQuality; quality++)
            {
                if (inventory.CountItems(name, quality) >= need)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Para recetas de un solo ingrediente: ¿hay alguna alternativa cubierta ya en el inventario?</summary>
        public static bool AnyIngredientCovered(Player player, Piece.Requirement[] requirements, int qualityLevel, int multiplier)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory inventory = player.GetInventory();

            foreach (Piece.Requirement requirement in requirements)
            {
                if (!IsRelevant(requirement, station))
                {
                    continue;
                }

                int need = requirement.GetAmount(qualityLevel) * multiplier;
                if (need > 0 && CoveredInSingleQuality(inventory, requirement, need))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trae del cofre mas cercano hacia afuera el material que falte. Siempre se agrega
        /// primero al inventario y recien despues se descuenta del cofre, para que un fallo
        /// no pueda perder items.
        /// </summary>
        public static void PullMissing(Player player, Recipe recipe, int qualityLevel, int itemQuality, int multiplier)
        {
            PullMissing(player, recipe.m_resources, qualityLevel, itemQuality, multiplier,
                recipe.m_requireOnlyOneIngredient);
        }

        public static void PullMissing(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier, bool onlyOneIngredient)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            Inventory playerInventory = player.GetInventory();
            bool pulledAnything = false;

            // En recetas de un solo ingrediente el juego consume uno cualquiera de los
            // materiales listados, y le exige a una sola calidad que cubra el total. Si ya hay
            // uno cubierto, no hay nada que traer.
            if (onlyOneIngredient && AnyIngredientCovered(player, requirements, qualityLevel, multiplier))
            {
                return;
            }

            foreach (Piece.Requirement requirement in requirements)
            {
                if (!IsRelevant(requirement, station))
                {
                    continue;
                }

                int need = requirement.GetAmount(qualityLevel) * multiplier;
                if (need <= 0)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int missing = need - playerInventory.CountItems(name, itemQuality);
                if (missing <= 0)
                {
                    continue;
                }

                // Con un solo ingrediente se trae solo el primero que alcance para completar
                // el requisito: sin este corte se vaciarian los cofres de todas las alternativas.
                if (onlyOneIngredient && CountAvailable(player, name, itemQuality) < missing)
                {
                    continue;
                }

                if (Pull(player, playerInventory, name, itemQuality, missing))
                {
                    pulledAnything = true;

                    if (onlyOneIngredient)
                    {
                        break;
                    }
                }
            }

            if (pulledAnything)
            {
                ContainerIndex.Invalidate();
            }
        }

        private static bool Pull(Player player, Inventory playerInventory, string name, int itemQuality, int missing)
        {
            Refresh(player);

            bool pulled = false;
            long playerId = ContainerAccess.LocalPlayerId();
            IReadOnlyList<Container> containers = ContainerIndex.Containers;

            // Excepcion al tope de escritura por accion: aca no hay uno explicito porque el
            // bucle corta apenas junta lo que falta (missing > 0) y la lista viene ordenada por
            // distancia, asi que en la practica toca uno o dos cofres. Ademas lo dispara un clic
            // del jugador, no un ciclo automatico.

            // Dos pasadas: primero los cofres que ya son nuestros. Tomar de esos no le quita la
            // propiedad a nadie, asi que reduce mucho las veces que hay que arrebatarla.
            for (int pass = 0; pass < 2 && missing > 0; pass++)
            {
                bool ownedOnly = pass == 0;

                for (int i = 0; i < containers.Count && missing > 0; i++)
                {
                    Container container = containers[i];

                    // La lista viene del cache, asi que puede tener hasta CacheMs de antiguedad:
                    // se revalida el acceso justo antes de escribir, por si otro jugador abrio el
                    // cofre en el intertanto.
                    if (!ContainerAccess.CanUse(container, playerId))
                    {
                        continue;
                    }

                    if (container.m_nview.IsOwner() != ownedOnly)
                    {
                        continue;
                    }

                    Inventory source = container.GetInventory();
                    if (source == null || source.CountItems(name, itemQuality) <= 0)
                    {
                        continue;
                    }

                    // Sin ser duenos del ZDO, quitar el item seria un cambio solo local que el
                    // proximo sync revierte: el jugador se quedaria con el material y el cofre
                    // tambien. Container solo llama a Save() cuando el cliente es el dueno.
                    container.m_nview.ClaimOwnership();

                    missing -= PullFrom(container, source, playerInventory, name, itemQuality, missing, out bool inventoryFull);
                    pulled = true;

                    if (inventoryFull)
                    {
                        return pulled;
                    }
                }
            }

            return pulled;
        }

        /// <summary>Saca de un cofre concreto, con un solo Save() al terminar.</summary>
        private static int PullFrom(Container owner, Inventory source, Inventory playerInventory, string name, int itemQuality, int missing, out bool inventoryFull)
        {
            inventoryFull = false;
            int taken = 0;

            // Igual que en el guardado rapido: se desconecta el callback para no serializar el
            // cofre una vez por stack retirado.
            Action previousHandler = source.m_onChanged;
            source.m_onChanged = null;

            try
            {
                List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(source.GetAllItems());

                foreach (ItemDrop.ItemData item in items)
                {
                    if (missing <= 0)
                    {
                        break;
                    }

                    if (item?.m_shared == null ||
                        item.m_shared.m_name != name ||
                        item.m_worldLevel < Game.m_worldLevel ||
                        (itemQuality >= 0 && item.m_quality != itemQuality))
                    {
                        continue;
                    }

                    int take = Mathf.Min(missing, item.m_stack);
                    ItemDrop.ItemData clone = item.Clone();
                    clone.m_stack = take;

                    // Inventory.AddItem puede meter parte del stack en huecos existentes y
                    // devolver false al quedarse sin casillas. Como lo que se agrega es una
                    // copia, descontar del cofre segun el valor de retorno duplicaria esas
                    // unidades: hay que descontar exactamente lo que entro.
                    int before = playerInventory.CountItems(name, itemQuality);
                    bool complete = playerInventory.AddItem(clone);
                    int added = playerInventory.CountItems(name, itemQuality) - before;

                    if (added > 0)
                    {
                        source.RemoveItem(item, added);
                        missing -= added;
                        taken += added;
                    }

                    if (!complete)
                    {
                        inventoryFull = true;
                        break;
                    }
                }
            }
            finally
            {
                source.m_onChanged = previousHandler;
            }

            if (taken > 0)
            {
                source.Changed();
                MoveLog.RecordSingle(intoContainer: false, owner, name, taken);
            }

            return taken;
        }
    }

    /// <summary>Habilita la receta cuando el material falta en el inventario pero esta en los cofres.</summary>
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    internal static class Player_HaveRequirementItems_Patch
    {
        private static void Postfix(Player __instance, Recipe piece, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (__result || discover || piece == null || !CraftFromContainers.Active(__instance))
            {
                return;
            }

            try
            {
                CraftingStation station = __instance.GetCurrentCraftingStation();
                Inventory inventory = __instance.GetInventory();
                bool onlyOne = piece.m_requireOnlyOneIngredient;

                foreach (Piece.Requirement requirement in piece.m_resources)
                {
                    if (!CraftFromContainers.IsRelevant(requirement, station))
                    {
                        continue;
                    }

                    int need = requirement.GetAmount(qualityLevel) * amount;
                    string name = requirement.m_resItem.m_itemData.m_shared.m_name;

                    // Una sola consulta O(1) al indice antes de entrar al bucle por calidad: si los
                    // cofres no tienen nada de este material no hay nada que sumar, y este postfix
                    // se ejecuta para cada receta cada vez que se refresca el panel de crafteo.
                    int inChests = CraftFromContainers.CanReceive(inventory, inventory.CountItems(name))
                        ? CraftFromContainers.InContainers(__instance, name, -1)
                        : 0;

                    // Igual que el vanilla: la cuenta se hace por calidad, sin mezclar calidades.
                    int best = 0;
                    for (int quality = 1; quality < requirement.m_resItem.m_itemData.m_shared.m_maxQuality + 1; quality++)
                    {
                        int total = inventory.CountItems(name, quality);
                        if (inChests > 0)
                        {
                            total += CraftFromContainers.InContainers(__instance, name, quality);
                        }

                        if (total > best)
                        {
                            best = total;
                        }
                    }

                    if (onlyOne)
                    {
                        if (best >= need)
                        {
                            __result = true;
                            return;
                        }
                    }
                    else if (best < need)
                    {
                        return;
                    }
                }

                __result = !onlyOne;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al evaluar requisitos con cofres: {e}");
            }
        }
    }

    /// <summary>Pinta el requisito como disponible cuando se cubre con los cofres.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class InventoryGui_SetupRequirement_Patch
    {
        private static void Postfix(Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier, bool __result)
        {
            // Hud.SetupPieceInfo llama a este mismo metodo para el menu de construccion, y ahi
            // 'craft' viene en false: ese flag es FreeBuildKey() == NoCraftCost, no "esto es
            // crafteo". Por eso quien decide es que panel esta en pantalla, no el flag.
            bool building = CraftFromContainers.IsBuilding(player);

            if (!__result || req.m_resItem == null || !PluginConfig.ShowContainerTotals.Value)
            {
                return;
            }

            if (building ? !CraftFromContainers.BuildActive(player) : (!craft || !CraftFromContainers.Active(player)))
            {
                return;
            }

            // Corre en cada frame desde InventoryGui.UpdateRecipe: una excepcion aca cortaria
            // el panel de crafteo del jugador por el resto de la sesion.
            try
            {
                int need = req.GetAmount(quality) * craftMultiplier;
                if (need <= 0)
                {
                    return;
                }

                string name = req.m_resItem.m_itemData.m_shared.m_name;
                int inInventory = player.GetInventory().CountItems(name);
                if (inInventory >= need)
                {
                    return;
                }

                if (inInventory + CraftFromContainers.CountAvailable(player, name, -1) < need)
                {
                    return;
                }

                Transform amountTransform = elementRoot.Find("res_amount");
                if (amountTransform == null)
                {
                    return;
                }

                TMP_Text amountText = amountTransform.GetComponent<TMP_Text>();
                if (amountText != null)
                {
                    amountText.color = CraftFromContainers.AvailableColor;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al pintar el requisito: {e}");
            }
        }
    }

    /// <summary>
    /// Trae el material de los cofres justo antes de que el juego craftee, de modo que el
    /// flujo vanilla completo se ejecuta con el inventario ya abastecido.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class InventoryGui_DoCrafting_Patch
    {
        private static bool Prefix(InventoryGui __instance, Player player)
        {
            if (!CraftFromContainers.Active(player) || __instance.m_craftRecipe == null)
            {
                return true;
            }

            try
            {
                Recipe recipe = __instance.m_craftRecipe;
                int qualityLevel = __instance.m_craftUpgradeItem == null ? 1 : __instance.m_craftUpgradeItem.m_quality + 1;
                int multiplier = __instance.m_multiCrafting ? __instance.m_multiCraftAmount : 1;

                // Si el producto no entra en el inventario, el vanilla va a abortar igual: no
                // tiene sentido haber vaciado los cofres para nada. Se deja correr al juego
                // para que muestre su propio mensaje.
                if (__instance.m_craftUpgradeItem == null && !recipe.m_requireOnlyOneIngredient &&
                    !player.GetInventory().CanAddItem(recipe.m_item.gameObject, recipe.m_amount * multiplier))
                {
                    return true;
                }

                CraftFromContainers.PullMissing(player, recipe, qualityLevel, -1, multiplier);

                // Recipe.GetAmount desreferencia sin chequear lo que devuelve
                // GetFirstRequiredItem, que exige que UNA calidad cubra el requisito. El
                // postfix de HaveRequirementItems habilito el boton contando los cofres, asi
                // que si el material no alcanzo a llegar hay que frenar aca: dejar seguir al
                // vanilla es una NullReferenceException dentro del juego.
                if (recipe.m_requireOnlyOneIngredient &&
                    !CraftFromContainers.AnyIngredientCovered(player, recipe.m_resources, qualityLevel, multiplier))
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_missingrequirement");
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Fallo al traer material de los cofres: {e}");
            }

            return true;
        }
    }
}
