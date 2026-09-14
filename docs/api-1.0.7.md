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

## Smelter (fundicion, horno de carbon, alto horno, molino, rueca, refineria de eitr)

Un solo componente cubre las seis estaciones: todas llevan `Smelter`.

| Miembro | Firma | Notas |
|---|---|---|
| `Awake` | `private void Awake()` | Hace `InvokeRepeating("UpdateSmelter", 1f, 1f)`: cadencia de 1 Hz gratis en todos los clientes |
| `UpdateSmelter` | `private void UpdateSmelter()` | Sale temprano si `!m_nview.IsOwner()`, pero un postfix corre igual: hay que repetir el chequeo de dueño |
| `IsItemAllowed` | `private bool IsItemAllowed(string itemName)` | Compara contra `m_conversion[].m_from.gameObject.name`, o sea el **nombre de prefab**, no `m_shared.m_name` |
| `GetFuel` / `SetFuel` | `private float GetFuel()` / `private void SetFuel(float)` | Leen y escriben `ZDOVars.s_fuel` |
| `GetQueueSize` | `private int GetQueueSize()` | Lee `ZDOVars.s_queued` |
| `m_fuelItem` | `public ItemDrop` | **Null en el horno de carbon**: ahi la madera entra como mineral, no como combustible |
| `m_maxOre`, `m_maxFuel` | `public int` | Topes de cola y de combustible |
| `m_nview` | `private ZNetView` | |

RPCs registrados en `Awake`:

```
m_nview.Register<string, bool>("RPC_AddOre", RPC_AddOre);   // nombre de prefab + cheated
m_nview.Register("RPC_AddFuel", RPC_AddFuel);               // suma 1 de combustible
```

Los dos actúan solo `if (m_nview.IsOwner())`. Siendo dueños, `InvokeRPC` se despacha **local y
sincronico** (ver `ZRoutedRpc.InvokeRoutedRPC`), asi que quitar del cofre y cargar el horno
ocurren en la misma pila.

## Construccion (Player)

| Miembro | Firma | Notas |
|---|---|---|
| `TryPlacePiece` | `public bool TryPlacePiece(Piece piece)` | Devuelve **antes** de `ConsumeResources`: es la ventana para abastecer el inventario |
| `HaveRequirements` | `public bool HaveRequirements(Piece piece, RequirementMode mode)` | Sobrecargada con la version de `Recipe`: el atributo de Harmony tiene que especificar los tipos |
| `RequirementMode` | `enum { CanBuild, IsKnown, CanAlmostBuild }` | `CanBuild` exige la cantidad completa; `CanAlmostBuild` solo que exista al menos uno |
| `InPlaceMode` | `public override bool InPlaceMode()` | `m_buildPieces != null` |

El flujo de `Player.UpdatePlacement` (~linea 1400):

```csharp
if (m_noPlacementCost || HaveRequirements(selectedPiece, RequirementMode.CanBuild))
    if (TryPlacePiece(selectedPiece))
        ...
        ConsumeResources(selectedPiece.m_resources, 0);   // qualityLevel 0, multiplicador 1
```

`Hud.SetupPieceInfo` (linea 1539) reutiliza `InventoryGui.SetupRequirement` para el menu de
construccion, con `quality: 0` y `craft: piece.FreeBuildKey() == GlobalKeys.NoCraftCost` — ese
flag **no** significa "esto es crafteo", asi que no sirve para distinguir los dos modos.

## Recipe

| Miembro | Firma | Notas |
|---|---|---|
| `GetAmount` | `public int GetAmount(int quality, out int need, out ItemDrop.ItemData singleReqItem, int craftMultiplier = 1)` | **Desreferencia `singleReqItem` sin chequear null** cuando `m_requireOnlyOneIngredient`. Si `GetFirstRequiredItem` devuelve null, tira NRE. El vanilla nunca llega porque el boton de craftear solo se habilita cuando una sola calidad cubre el requisito |

## ZDOMan

| Miembro | Notas |
|---|---|
| `ReleaseNearbyZDOS` | Cada 2 s reasigna la propiedad de los ZDO al jugador en cuya area activa estan, pero **solo si estan sin dueño o el dueño actual ya no los tiene en su area**. O sea: la propiedad es pegajosa, no migra al mas cercano mientras el dueño siga en rango |
| `GetSessionID` | `public static long GetSessionID()` |

Consecuencia practica: en un cliente, `ZNet.instance.GetPeer(uid)` **no ve a los otros
clientes** (la lista de peers de un cliente solo tiene al servidor), asi que no sirve para
saber si el dueño de un ZDO es un jugador conectado.

## MonsterAI / BaseAI / Tameable (alimentacion de domesticados)

| Miembro | Firma | Notas |
|---|---|---|
| `BaseAI.UpdateAI` | `public virtual bool UpdateAI(float dt)` | **Sale temprano si `!m_nview.IsOwner()`**: toda la rama de IA corre solo en el cliente dueno. Da la eleccion de "un solo alimentador" gratis |
| `MonsterAI.UpdateConsumeItem` | `private bool UpdateConsumeItem(Humanoid, float dt)` | Cada `m_consumeSearchInterval` (10 s) y solo si `m_tamable.IsHungry()` |
| `MonsterAI.FindClosestConsumableItem` | `private ItemDrop FindClosestConsumableItem(float maxRange)` | `Physics.OverlapSphere` sobre la capa "item": **solo ve objetos en el suelo**, nunca dentro de cofres |
| `MonsterAI.CanConsume` | `private bool CanConsume(ItemDrop.ItemData item)` | Compara contra `m_consumeItems` por `m_shared.m_name`. Es la lista de comida propia de cada bicho |
| `MonsterAI.m_consumeRange` / `m_consumeSearchRange` / `m_consumeSearchInterval` | `public float` | 2 / 5 / 10 por defecto |
| `BaseAI.m_tamable` | `protected Tameable` | Null en los bichos salvajes |
| `BaseAI.HavePath` | `protected bool HavePath(Vector3 target)` | |
| `Tameable.IsTamed` / `IsHungry` | `public bool` | |
| `ItemDrop.DropItem` | `public static ItemDrop DropItem(ItemData item, int amount, Vector3 position, Quaternion rotation)` | Clona el item internamente y fija `m_stack = amount` si `amount > 0` |
| `ItemDrop.RemoveOne` | `public bool RemoveOne()` | Lo que usa el animal para comer |

El enganche del mod es un postfix de `FindClosestConsumableItem`: cuando el vanilla busco y no
encontro nada en el suelo, se saca una unidad del cofre y se tira al piso, y el resto del flujo
vanilla (caminar, comer, resetear hambre, progreso de cria) sigue igual. Es autolimitante: como
el postfix solo corre si no hay comida cerca, nunca puede acumular comida tirada.

