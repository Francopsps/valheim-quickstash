---
name: feedback-confirmed-non-findings
description: Falsos positivos ya refutados con evidencia en QuickStash — no volver a reportarlos sin evidencia nueva
metadata:
  type: feedback
---

Refutados con decompilacion, no re-reportar salvo que el codigo cambie.
**Why:** cada uno costo una decompilacion; volver a levantarlos quema presupuesto y le hace
perder confianza al usuario en el reporte.
**How to apply:** spot-check que el guard siga presente, pero no listarlos como faltantes.

- **StationFeeder NO puede sobrecargar un horno.** `FeedFuel`/`FeedOre` calculan `room` antes
  del bucle y el RPC a un ZDO propio es sincronico, asi que `GetFuel()`/`GetQueueSize()` estan
  frescos entre items y entre cofres. Los topes coinciden exactamente con `OnAddFuel`/`OnAddOre`
  vanilla, incluido el borde fraccionario del fuel. Ver [[verified-vanilla-invariants]].
- **Los `InvokeRPC` del auto-alimentado no generan trafico de red.** Van a un ZDO propio →
  despacho local en la misma pila. El costo de red es el `Container.Save()` del cofre, no los RPC.
- **`StashService.StackInto` mide bien el movimiento parcial** (`stackBefore - item.m_stack`
  en la rama `AddItem == false`) y mueve el objeto original, no un clon. Correcto.
- **`CraftFromContainers.PullFrom` descuenta por delta real de `CountItems`**, agrega antes de
  quitar, y filtra `m_worldLevel`. Correcto.
- **`item?.m_shared` NO viola la regla del null de Unity**: `ItemDrop.ItemData` y `SharedData`
  son clases planas `[Serializable]`, no derivan de `UnityEngine.Object`. Los chequeos sobre
  `m_dropPrefab`, `Player`, `Container`, `Smelter` si usan `== null` (correcto).
- **`Plugin.PatchGroup` aisla por tipo de parche con try/catch**: si el juego renombra un
  metodo privado (`UpdateSmelter`, `DoCrafting`, `Awake`) se cae solo esa feature, no el plugin.
- **`BuildFromContainers.HasStationAndDlc` es mas restrictivo que el vanilla para
  `CanAlmostBuild`** (usa `HaveBuildStationInRange` donde vanilla usa `m_knownStations`).
  Direccion segura: nunca habilita de mas. Solo un detalle cosmetico del menu de construccion.
