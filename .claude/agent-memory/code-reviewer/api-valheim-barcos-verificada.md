---
name: api-valheim-barcos-verificada
description: Hechos del vanilla sobre Ship/ZNetView/Player decompilados y confirmados — evita volver a decompilar lo mismo.
metadata:
  type: reference
---

Decompilado de `refs/Managed/assembly_valheim.dll` (Valheim 1.0), confirmado 2026-09-14.
Reverificar si cambia la version del juego.

- `Ship.CustomFixedUpdate(float fixedDeltaTime)` — **publico**, parametro `fixedDeltaTime`.
  Lo llama `MonoUpdaters.FixedUpdate` recorriendo `Ship.Instances` de forma **secuencial**,
  un barco por vez: no hay reentrada entre barcos.
- `Ship.Start()` es **privado** (registra "Stop"/"Forward"/"Backward"/"Rudder" + `InvokeRepeating`).
- Toda la fuerza de remo sale de dos lineas: `Speed.Slow` suma `forward * m_backwardForce * (1-|rudder|)`
  y `Speed.Back` la misma con signo negativo. Ambas dentro de la rama "estoy en el agua".
- Esa rama es la unica que llama `UpdateWaterForce(depth, Time.time)`, que deja
  `m_lastUpdateWaterForceTime = Time.time`. Comparar contra `Time.time` en un postfix es una
  forma valida de saber si el barco estaba en el agua ese tick.
- `CustomFixedUpdate` solo **baja** `m_speed` a `Stop` (sin jugadores, o Slow/Back sin timonel);
  nunca la sube. Leer `m_speed` en un prefix no puede perder empuje.
- `UpdateControlls`: el dueño escribe `m_speed` al ZDO; el que no es dueño lo **lee** del ZDO.
  Por eso `GetSpeedSetting()` es valido en cualquier cliente.
- `ZNetView.InvokeRPC(string, params object[])` enruta a `m_zdo.GetOwner()`. Si no hay dueño
  devuelve 0, **y 0 es `ZNetView.Everybody`** → broadcast accidental. Hay que guardar con `HasOwner()`.
- `ZNetView.HandleRoutedRPC` hace `ZLog.LogWarning("Failed to find rpc method " + hash)` cuando
  el receptor no tiene el RPC registrado: un mod cliente que manda RPCs propios **ensucia el log
  de los jugadores que no lo tienen instalado**.
- `ZNetView.Register` usa `m_functions.Add` (no el indexer): registrar dos veces el mismo nombre tira.
- `Player.RPC_UseStamina` hace `m_staminaRegenTimer = m_staminaRegenDelay` en **cada** cobro no
  nulo: cobrar stamina seguido apaga la regeneracion entera, no cuesta "un poco".
- `Character.GetOwner()` devuelve el dueño del ZDO → sirve para validar identidad del remitente de un RPC.
- `Ship.GetLocalShip()` devuelve `s_currentShips[Count-1]` (el ultimo abordado). `s_currentShips`
  se limpia en `OnTriggerExit` y `OnDestroyed`, **no** en `OnDisable`/`OnDestroy`: puede quedar
  una referencia destruida → siempre chequear con `== null` (sobrecarga de Unity).
