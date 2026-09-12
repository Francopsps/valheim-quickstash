# API de Valheim 1.0 verificada (build del 11-sep-2026)

Firmas confirmadas decompilando `refs/Managed/assembly_valheim.dll` con `ilspycmd`.
Valheim 1.0 movió parte de la API interna, así que este documento existe para no volver a
adivinar: cuando el juego se parchee, se revalida esta lista antes de tocar código.

Para regenerar los volcados:

```bash
ilspycmd refs/Managed/assembly_valheim.dll -t Container
```

## Container

| Miembro | Firma | Notas |
|---|---|---|
| `Awake` | `private void Awake()` | Punto de registro del mod. Solo inicializa si `m_nview.GetZDO() != null` |
| `OnDestroyed` | `private void OnDestroyed()` | Solo se dispara al destruir el cofre, **no** al descargar la zona. Por eso el registro poda en caliente las entradas nulas |
| `CheckAccess` | `private bool CheckAccess(long playerID)` | `Public` → true, `Private` → solo el creador, `Group` → false |
| `IsInUse` | `public bool IsInUse()` | Devuelve `m_inUse`, que **solo es válido en el dueño**. Para cofres remotos hay que leer `ZDOVars.s_inUse` del ZDO |
| `GetInventory` | `public Inventory GetInventory()` | |
| `Save` / `Load` | `private` | `Save()` escribe `ZDOVars.s_items`. `Load()` refresca desde el ZDO y lo llama `CheckForChanges` cada 1 s |
| `OnContainerChanged` | `private` | Enganchado a `m_inventory.m_onChanged`: llama `Save()` **solo si el cliente es dueño** |
| `StackAll` | `public void StackAll()` | Manda `RPC_RequestStack` al dueño |
| `m_nview`, `m_inventory`, `m_inUse` | privados | Accesibles vía publicizer |

### Protocolo de propiedad (el que usa el mod)

```
cliente  --RPC_RequestStack(playerID)-->  dueño
                                          valida IsInUse() y CheckAccess()
                                          ForceSendZDO + ZDO.SetOwner(uid)
cliente  <--RPC_StackResponse(bool)----   dueño
```

Es la vía correcta para tocar un cofre ajeno: el dueño valida y cede el control. Alternativa
unilateral: `ZNetView.ClaimOwnership()`, que es lo que usa el propio juego en
`RPC_TakeAllResponse` y lo único posible cuando hace falta una escritura síncrona.

## Inventory

| Miembro | Firma |
|---|---|
| `StackAll` | `public int StackAll(Inventory fromInventory, bool message = false)` |
| `AddItem` | `public bool AddItem(ItemDrop.ItemData item)` — llena stacks existentes y después busca hueco |
| `RemoveItem` | `public bool RemoveItem(ItemDrop.ItemData item, int amount)` |
| `CountItems` | `public int CountItems(string name, int quality = -1, bool matchWorldLevel = true)` — `name == null` cuenta todo; filtra por `m_worldLevel >= Game.m_worldLevel` |
| `ContainsItemByName` | `public bool ContainsItemByName(string name)` |
| `GetAllItems` | `public List<ItemDrop.ItemData> GetAllItems()` |
| `HaveEmptySlot` | `public bool HaveEmptySlot()` |
| `Changed` | `private void Changed(bool success = false, bool cheatedStateChanged = false)` |
| `m_onChanged` | `public Action m_onChanged` |

`StackAll` vanilla solo mueve ítems cuyo nombre ya existe en el destino y que no estén
equipados. El mod replica esa regla y le suma favoritos, barra rápida y exclusiones.

## InventoryGui

| Miembro | Firma |
|---|---|
| `m_player`, `m_container`, `m_crafting` | `public RectTransform` |
| `m_stackAllButton`, `m_takeAllButton`, `m_dropButton` | `public Button` |
| `m_playerGrid` | `public InventoryGrid` |
| `m_currentContainer` | `private Container` |
| `m_dragItem` | `private ItemDrop.ItemData` |
| `SetupRequirement` | `public static bool SetupRequirement(Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier = 1)` |
| `DoCrafting` | `private void DoCrafting(Player player)` |
| `m_craftRecipe`, `m_craftUpgradeItem`, `m_multiCrafting`, `m_multiCraftAmount` | privados |
| `IsVisible` | `public static bool IsVisible()` |

`DoCrafting` consume con `player.ConsumeResources(m_craftRecipe.m_resources, num, -1, multiplier)`,
donde `num` es el nivel de calidad y `multiplier` es `m_multiCrafting ? m_multiCraftAmount : 1`.

## InventoryGrid

| Miembro | Firma |
|---|---|
| `OnLeftDown` | `private void OnLeftDown(UIInputHandler clickHandler)` — acá se decide agarrar/mover; Shift = Split, Ctrl = Move (por eso el mod usa Alt) |
| `UpdateGui` | `private void UpdateGui(Player player, ItemDrop.ItemData dragItem)` — **corre cada frame** mientras la GUI está abierta |
| `GetButtonPos` | `private Vector2i GetButtonPos(GameObject go)` |
| `m_elements` | `private List<InventoryElement>` |
| `m_inventory` | `private Inventory` |

`InventoryElement` expone `Position` (`Vector2i`), `m_icon`, `m_equiped`, `m_queued`, `m_selected`.

## Player

| Miembro | Firma |
|---|---|
| `HaveRequirements` | `public bool HaveRequirements(Recipe recipe, bool discover, int qualityLevel, int amount = 1)` |
| `HaveRequirementItems` | `private bool HaveRequirementItems(Recipe piece, bool discover, int qualityLevel, int amount = 1)` — cuenta por calidad y toma el máximo, sin mezclar calidades |
| `ConsumeResources` | `public void ConsumeResources(Piece.Requirement[] requirements, int qualityLevel, int itemQuality = -1, int multiplier = 1)` |
| `GetCurrentCraftingStation` | `public CraftingStation GetCurrentCraftingStation()` — devuelve `m_currentStation`, no nulo solo al estar en una estación |
| `OnSpawned` | `public void OnSpawned(bool spawnValkyrie)` |
| `IsItemEquiped` | `public bool IsItemEquiped(ItemDrop.ItemData item)` (heredado de `Humanoid`) |

## Otros

| Miembro | Firma |
|---|---|
| `ZNetView.InvokeRPC` | `public void InvokeRPC(string method, params object[] parameters)` — enruta al dueño del ZDO |
| `ZNetView.ClaimOwnership` | `public void ClaimOwnership()` |
| `ZDO.GetInt` | `public int GetInt(int hash, int defaultValue = 0)` |
| `ZDOVars.s_inUse` / `s_items` | `public static readonly int` |
| `PrivateArea.CheckAccess` | `public static bool CheckAccess(Vector3 point, float radius = 0f, bool flash = true, bool wardCheck = false)` |
| `PlayerProfile.GetFilename` | `public string GetFilename()` — clave por personaje |
| `Game.Logout` | `public void Logout(bool save = true, bool changeToStartScene = true)` |
| `Chat.HasFocus` | `public bool HasFocus()` |
| `Localize` | MonoBehaviour en `assembly_guiutils`, relocaliza el subárbol al cambiar idioma |
