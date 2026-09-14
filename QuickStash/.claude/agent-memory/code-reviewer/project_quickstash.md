---
name: project-quickstash
description: Estado e historial de reviews de QuickStash (mod cliente de Valheim para servidor de 14 jugadores) — que se reviso, que quedo abierto.
metadata:
  type: project
---

QuickStash es un mod 100% cliente para un servidor privado de ~14 jugadores hispanohablantes.

**Why:** el objetivo es que el mod se pueda repartir entre amigos sin que nadie pierda items ni se
le rompa la UI; por eso el listón es "duplicar o perder objetos" y "romper el inventario", no
elegancia. Filosofia KISS explicita: ~1000 lineas, sin Jotunn, sin AssetBundles.

**How to apply:** cada review se cierra con veredicto derivado del conteo de criticos, no de
impresion. Lo que no se puede verificar estaticamente va al protocolo de prueba in-game, nunca se
presenta como verificado.

## Historial

- **2026-09-13 — review de 1.1.0** (`Features/StationFeeder.cs` y `Features/BuildFromContainers.cs`
  nuevos, refactor de `CraftFromContainers`, split de tope de lectura/escritura en
  `ContainerRegistry`). 0 CRITICAL, 2 WARNING, 5 INFO. Veredicto: repartir tras corregir los 2
  warnings. Los dos warnings fueron:
  1. `InPlaceMode()` usado como "esta construyendo" en `CraftFromContainers` (ver
     [[valheim-api-facts]]: significa "martillo equipado").
  2. `StationFeeder.FeedFrom` deja `source.Changed()` fuera del `try/finally` del detach de
     `m_onChanged` → ventana de duplicacion si algo tira en el medio.
  Verificado y correcto (no volver a marcar): binding de los tres parches nuevos, espejo exacto de
  los topes de fuel/mineral del vanilla, ausencia de `?.` sobre `UnityEngine.Object` en todo el repo,
  y que el postfix de `HaveRequirements` no es un bucle por-frame de N piezas.
