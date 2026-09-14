---
name: valheim-vanilla-gotchas
description: Comportamientos de Valheim 1.0.7 verificados decompilando — usalos sin volver a decompilar, pero re-verifica si cambia la version del juego.
metadata:
  type: reference
---

Verificado con `ilspycmd refs/Managed/assembly_valheim.dll -t <Tipo>` el 2026-09-13 (juego 1.0.7).

- `Container.OnContainerChanged()` → `if (!m_loading && IsOwner()) Save()`. Confirma el modelo de propiedad.
- `Inventory.RemoveItem(ItemData, int)`: si `amount == item.m_stack` delega en `RemoveItem(item)` (que valida `Contains` y loguea "Item is not in this container"); si no, valida `Contains`, resta y llama `Changed()`. **Ambas ramas son seguras ante referencias rancias.**
- `ItemDrop.DropItem(...)` **nunca devuelve null**: hace `Instantiate(...).GetComponent<ItemDrop>()` y deref inmediato. O tira excepcion o devuelve objeto. Chequear `== null` en el retorno es codigo muerto.
- `ItemDrop.TimedDestruction()` solo destruye si: >3600 s de vida **Y** `!IsInsideBase()` **Y** ningun jugador a 25 m **Y** `!InTar()` **Y** `!IsPiece()`. `IsInsideBase()` = `y > 28f && EffectArea PlayerBase`. ⇒ **items tirados dentro de una base NO despawnean nunca.**
- `ItemDrop.AutoStackItems()` solo corre cuando `s_instances.Count > 200` local, radio 4 m, una sola vez por instancia (`m_haveAutoStacked`).
- `MonsterAI.FindClosestConsumableItem` usa `Physics.OverlapSphere` **sin tope**; exige `ZNetView.IsValid()` y `HavePath()`. `m_consumeSearchInterval` default 10 s, `m_consumeSearchRange` 5 m, `m_consumeRange` 2 m.
- `MonsterAI.UpdateConsumeItem` solo se alcanza si `(!IsAlerted() || (m_targetStatic == null && m_targetCreature == null))` ⇒ **durante un raid los domesticados no comen.**
- `Tameable.IsHungry()` lee `ZDOVars.s_tameLastFeeding` del ZDO (sirve sin ser dueño) y llama `ZNet.instance.GetTime()` (NRE si ZNet esta caido).
- `Procreation`: cap vanilla `m_maxCreatures = 4` dentro de `m_totalCheckRange = 10 m`; no procrea si `m_tameable.IsHungry()`.
- `BaseAI.m_instances` es `private static List<BaseAI>` (accesible via Krafs.Publicizer), Add en `Awake` / Remove en `OnDestroy` ⇒ un animal **muerto pero no destruido** sigue en la lista.
- `Player.GetAllPlayers()` devuelve `s_players`: solo jugadores **instanciados localmente**.

Relacionado: [[valheim-decompile-workflow]]
