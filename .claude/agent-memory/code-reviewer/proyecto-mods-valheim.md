---
name: proyecto-mods-valheim
description: El repo "J:\Mod Valheim" aloja varios mods cliente hermanos (QuickStash maduro, CrewRow nuevo) que comparten convenciones, build y agentes por mod.
metadata:
  type: project
---

El repositorio aloja **varios mods**, no uno solo. Cada mod es una carpeta hermana con su
propio `src/`, `build.ps1`, `package/`, `docs/` y `agentes/` (commit 4637a37, "cada mod queda
con sus propios docs y agentes").

- **QuickStash** (v1.3.3): el maduro. Es la referencia de convenciones para los demas.
- **CrewRow** (v0.1.0, revisado 2026-09-14): remada de tripulacion en barcos. Cliente puro,
  3 parches (`Ship.Start`, `Ship.CustomFixedUpdate`, `Game.Logout`), ~1290 lineas.

**Why:** el prompt del agente describe solo QuickStash, asi que al recibir un scope nuevo hay
que confirmar de que mod se trata antes de aplicar rutas o supuestos.

**How to apply:** `docs/api-1.0.7.md` esta en `QuickStash/docs/`; CrewRow tiene su propio
`CrewRow/docs/api-barcos-1.0.7.md`. Cada mod tiene su `docs/pruebas.md` con el protocolo
in-game ya escrito — leerlo antes de proponer pruebas, para no duplicar lo que ya esta.
Relacionado: [[convenciones-verificadas-repo]], [[falsos-positivos-descartados]].
