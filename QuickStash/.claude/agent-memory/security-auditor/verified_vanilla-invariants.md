---
name: verified-vanilla-invariants
description: Hechos del vanilla de Valheim 1.0 confirmados decompilando — caros de re-derivar, usar como base en auditorias futuras
metadata:
  type: reference
---

Confirmados leyendo `refs/Managed/assembly_valheim.dll` (ver [[reference-valheim-decompile]]).
Fecha de verificacion: 2026-09-13, version 1.0.7.

**Red / propiedad de ZDO**
- `ZNet.GetUID()` == `ZDOMan.GetSessionID()` == `ZRoutedRpc.m_id` (`ZNet.cs:382` hace
  `m_routedRpc.SetUID(ZDOMan.GetSessionID())`). `ZDO.SetOwner` guarda ese mismo session id.
- Consecuencia clave: `nview.InvokeRPC("X", ...)` sobre un ZDO **que ya es nuestro** llama a
  `HandleRoutedRPC` **en la misma pila y NO manda nada por la red**. El handler corre antes de
  que `InvokeRPC` retorne, asi que los ZDO que toque quedan frescos para la linea siguiente.
  Si el ZDO no tiene dueno (owner 0) el mismo camino hace **broadcast a todos los peers**.

**Inventario**
- `Inventory.AddItem(ItemData)`: llena stacks existentes de a una unidad; al primer hueco
  hace `item.m_stack = restante`, intenta UN slot y **siempre `break`**. Devuelve false si no
  habia slot, pero puede haber fundido parte del stack. Mide siempre el delta real.
- `Inventory.RemoveItem(ItemData, int)`: si `amount == item.m_stack` saca el objeto de la
  lista; con stack ya en 0 devuelve **false**.
- `Inventory.RemoveItem(string, int, quality, worldLevelBased=true)`: **es parcial y silenciosa**
  — no devuelve nada y no avisa si no llego a descontar todo. Es la que usa `ConsumeResources`.
- `CountItems(name, quality=-1, matchWorldLevel=true)` filtra por `Game.m_worldLevel`.

**Container**
- `OnContainerChanged()` → `if (!m_loading && IsOwner()) Save()`. `Save()` serializa el
  inventario COMPLETO a `ZDOVars.s_items`.
- `Load()` sale temprano si `DataRevision == m_lastRevision` o si `m_inUse`.

**Construccion**
- `Player.TryPlacePiece` tiene **un solo call site** (`Player.UpdatePlacement`) y **no
  revalida materiales**: solo `m_placementStatus`. Devuelve true y ya instancio la pieza.
- Orden vanilla: `HaveRequirements(piece, CanBuild)` → `TryPlacePiece` → (si no hay global key
  `FreeBuildKey()`) `ConsumeResources(piece.m_resources, 0)`.
- `Piece.Requirement.GetAmount(0)` == `m_amount`.
- `Player.HaveRequirements(Piece, CanBuild)` cuenta con `m_inventory.CountItems(name)`;
  `CanAlmostBuild` usa `m_knownStations` (no `HaveBuildStationInRange`).

**Smelter** (cubre fundicion, kiln, alto horno, molino, rueca, refineria de eitr)
- `InvokeRepeating("UpdateSmelter", 1f, 1f)` en TODOS los clientes; el bloque de produccion
  solo corre en el dueno.
- `RPC_AddFuel` / `RPC_AddOre` **no tienen tope**: el limite (`m_maxFuel`, `m_maxOre`) lo
  aplica solo el lado cliente en `OnAddFuel`/`OnAddOre`. Vanilla rechaza fuel si
  `GetFuel() > m_maxFuel - 1` y ore si `GetQueueSize() >= m_maxOre`.
- `IsItemAllowed(string)` compara contra `m_conversion[].m_from.gameObject.name` (nombre de
  **prefab**, no `m_shared.m_name`).
