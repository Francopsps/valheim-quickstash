---
name: valheim-api-facts
description: Hechos del assembly_valheim.dll 1.0.7 ya verificados por decompilacion, que evitan repetir el trabajo y previenen falsos positivos recurrentes en los reviews de QuickStash.
metadata:
  type: reference
---

Verificados con `ilspycmd refs/Managed/assembly_valheim.dll -t <Tipo>`. Re-verificar solo si el
juego sube de version (`docs/api-1.0.7.md` es la fuente para 1.0.7).

**Player**
- `InPlaceMode() => m_buildPieces != null`. `m_buildPieces` lo setea `SetPlaceMode(...)`, invocado
  solo desde el refresco de equipo segun `m_rightItem.m_shared.m_buildPieces`. **Significa "martillo
  equipado", NO "el menu de construccion esta en pantalla"**. Puede ser true con el panel de crafteo
  abierto. Discriminador correcto para "esta craftendo": `InventoryGui.IsVisible()` (public static).
- `HaveRequirements(Piece piece, RequirementMode mode)` — nombres de parametro `piece`, `mode`.
  Orden interno: estacion (`m_knownStations.ContainsKey` para IsKnown/CanAlmostBuild,
  `HaveBuildStationInRange` para CanBuild) -> DLC -> `FreeBuildKey()` global -> materiales.
- `TryPlacePiece(Piece piece)` es publico, ya instancio la pieza cuando devuelve true, y
  `ConsumeResources` corre despues en `UpdatePlacement`.
- `UpdateAvailablePiecesList()` llama `UpdateAvailable(..., hideUnavailable: false, ...)`
  **hardcodeado**: la rama `CanAlmostBuild` de `PieceTable.UpdateAvailable` esta muerta en 1.0.

**Hud**
- `UpdatePieceBuildStatus` evalua **un solo icono por frame** (`m_pieceIconUpdateIndex++`). El barrido
  completo (`UpdatePieceBuildStatusAll`) es solo al abrir/cambiar de categoria. No es un bucle
  por-frame de N piezas.
- `SetupPieceInfo` llama `InventoryGui.SetupRequirement(..., craft: piece.FreeBuildKey() == NoCraftCost, 0)`.
  Ese `craft` **no significa "esto es crafteo"**; `Piece.FreeBuildKey()` devuelve `NoCraftCost` solo si
  la pieza tiene `ItemDrop` o `Feast`, si no `NoBuildCost`.

**Smelter** (cubre fundicion, horno de carbon, alto horno, molino, rueca, refineria de eitr)
- `Awake` hace `InvokeRepeating("UpdateSmelter", 1f, 1f)` en todos los clientes.
  `UpdateSmelter` es `private void` sin parametros.
- `GetFuel()`, `GetQueueSize()`, `IsItemAllowed(string)` son privados (los expone Publicizer).
  `IsItemAllowed` compara contra `m_conversion[].m_from.gameObject.name` = nombre de prefab.
- Topes vanilla: fuel se rechaza si `GetFuel() > m_maxFuel - 1`; mineral si `GetQueueSize() >= m_maxOre`.

**Red / inventario**
- `ZRoutedRpc.InvokeRoutedRPC` llama `HandleRoutedRPC` **sincronico en la misma pila** cuando
  `targetPeerID == m_id`. O sea: siendo dueno del ZDO, `nview.InvokeRPC(...)` aplica el efecto antes
  de volver. Se puede confiar en releer el estado del ZDO dentro del mismo bucle.
- `Inventory.RemoveItem(item, amount)` devuelve false solo si el item ya no esta en la lista; si
  `amount == item.m_stack` saca el objeto de la lista pero **no pone `m_stack` en 0**.
- `PrivateArea.CheckAccess` hace `new List<PrivateArea>()` en cada llamada (basura vanilla, no del mod).

Ver [[project-quickstash]].
