---
name: quickstash-settled-design
description: QuickStash design decisions already litigated — do NOT re-report these as findings in future security audits.
metadata:
  type: project
---

These are settled, deliberate choices in QuickStash. Reporting them as findings wastes the orchestrator's time.

- **Solo se usan cofres cuyo ZDO ya es nuestro** en los ciclos automaticos (StationFeeder, AnimalFeeder). Nunca `ClaimOwnership()` ahi.
- **Se descuenta del cofre ANTES de instanciar/cargar.** Prefieren perder una unidad a duplicarla. No propongas invertir el orden.
- **La comida de animales cae al lado del animal, no del cofre** (el cofre puede estar fuera del cerco).
- **No se parchea `Player.ConsumeResources`** — para no chocar con el mod CraftFromContainers de terceros.
- El guardado rapido usa el protocolo vanilla `RPC_RequestStack`/`RPC_StackResponse` con pendientes + timeout.
- `Plugin.ApplyPatches` parchea **por grupo de feature**, no `PatchAll` global, a proposito: un rename en el juego apaga una feature en vez de tumbar el plugin.

**Why:** el orquestador las enumero explicitamente como cerradas en la auditoria de v1.3.0 (2026-09-13).
**How to apply:** si un hallazgo tuyo se reduce a una de estas, degradalo a INFO o descartalo; buscá en cambio el *incumplimiento* del invariante (ej.: un camino que SI arrebata propiedad).

Relacionado: [[quickstash-verified-invariants]], [[valheim-decompile-workflow]]
