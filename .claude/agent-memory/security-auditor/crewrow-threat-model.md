---
name: crewrow-threat-model
description: Modelo de amenaza y controles verificados del mod CrewRow (remar en barcos) — que NO re-auditar y donde estan los puntos debiles reales.
metadata:
  type: project
---

CrewRow es el segundo mod del workspace (`CrewRow/src/`), v0.1.0, auditado por primera vez el 2026-09-14.

**Why:** su modelo de amenaza es **distinto al de QuickStash**. No mueve items, no toca inventarios ni contenedores, no escribe campos propios en ZDOs, no escribe a disco, no usa corrutinas ni hilos. Toda la familia duplicacion/perdida de items **no aplica**.

**How to apply:** al auditar CrewRow, saltear integridad de items y escritura a disco. El riesgo vive en cuatro lugares:
1. El RPC custom `CrewRow_Rowing` registrado en el ZNetView del barco (`RowerRegistry.Register`, desde un postfix de `Ship.Start`). Es el **unico RPC propio del workspace** — ver [[valheim-vanilla-gotchas]], seccion capa de red: `sender` es forjable.
2. Diccionarios alimentados por red (`States` / `Rowers` / `Budgets` en `RowerRegistry`). El orden de las validaciones respecto de `TakeBudget` decide si el keyspace esta acotado.
3. `RowBoost` escala `Ship.m_backwardForce` en un prefix y lo restaura en postfix + Finalizer. La restauracion de un solo mod es solida; el riesgo es la **interaccion con otro mod** que haga snapshot/restore del mismo campo (Harmony corre los postfix en orden de prioridad, **no** en orden inverso a los prefix).
4. `RowBoost.Notify` → `Player.Message` sobre el timonel remoto: es un `InvokeRPC` disfrazado.

Controles ya presentes y verificados OK: patcheo por feature aislado (`Plugin.PatchGroup`), guarda `HasOwner()` en las dos rutas de envio de `RowerClient`, `try/catch` en todos los parches y en `Update`, `AcceptableValueRange` en todos los valores numericos, `WarnAboutConflicts`, reset en `Game.Logout`, cero `?.`/`??` sobre tipos de Unity.

Relacionado: [[quickstash-settled-design]]
