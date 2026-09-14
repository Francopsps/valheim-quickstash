---
name: quickstash-verified-invariants
description: Invariantes de QuickStash verificados leyendo codigo + decompilado — spot-check, no re-auditar desde cero.
metadata:
  type: project
---

Verificados por lectura directa en la auditoria de v1.3.0 (2026-09-13). Re-verificar con spot-check, no desde cero.

- `ContainerAccess.CanUse`: nview valido → `IsInUse` leido del **ZDO** para remotos → `CheckAccess(playerId)` → `PrivateArea.CheckAccess` si `m_checkGuardStone`. Correcto y completo.
- `ContainerRegistry.Query` llama `CanUse` **inline en cada consulta** (no hay TTL ahi; el cache con TTL vive en `ContainerIndex`). O sea: las escrituras de StationFeeder/AnimalFeeder revalidan permisos frescos. No reportar "cache decide escrituras".
- `StationFeeder`: `source.Changed()` dentro del `finally`, tope global `FeedMaxStationsPerSecond`, y el postfix de `Smelter.UpdateSmelter` envuelto en try/catch. (fixes de 1.1.0, intactos en 1.3.0)
- `Player_TryPlacePiece_Patch`: prefix con revalidacion `CoveredByInventory` + chequeo de `FreeBuildKey` + try/catch.
- **Ningun `?.`/`??` sobre tipos `UnityEngine.Object`** en todo `src/`. Todos los usos son sobre `ItemDrop.ItemData`, `string` o `Harmony`, que son clases normales. Ya lo barri entero.
- `ItemDrop.DropItem` preserva `m_worldLevel` porque hace `item.Clone()` y **no** llama `OnCreateNew`.

**Why:** evitar que cada auditoria vuelva a gastar turnos en lo mismo.
**How to apply:** empezá por el codigo nuevo del diff; a estos dales un grep de confirmacion y seguí.

Relacionado: [[quickstash-settled-design]], [[valheim-vanilla-gotchas]]
