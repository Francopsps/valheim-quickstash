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

## Capa de red (verificado 2026-09-14, ZRoutedRpc / ZNetView / ZNet / ZDO)

- **`sender` de un RPC ruteado es FORJABLE.** `ZRoutedRpc.InvokeRoutedRPC` pone `m_senderPeerID = m_id` en el *emisor*, y `RouteRPC` en el servidor re-serializa el paquete tal cual, sin sobreescribirlo. ⇒ **ningun mod puede tratar `sender` como identidad autenticada.** Es lo mas importante a saber para auditar cualquier RPC custom.
- `ZNetView.InvokeRPC(metodo, args)` sin destinatario → `InvokeRoutedRPC(m_zdo.GetOwner(), ...)`. `ZDO.GetOwner()` devuelve `0L` si `!Owned`, y `0L == ZRoutedRpc.Everybody` ⇒ **broadcast a todo el servidor**. Siempre guardar con `nview.HasOwner()`.
- `Player.Message(...)` sobre un Player **remoto** hace `m_nview.InvokeRPC("Message", ...)` ⇒ hereda el mismo riesgo de broadcast. Mandarle un mensaje a otro jugador NO es una operacion local.
- `ZNetView.Register(name, f)` usa `m_functions.Add(hash, ...)` (no el indexador) ⇒ registrar dos veces el mismo nombre en el mismo nview **tira ArgumentException**.
- `ZNetView.HandleRoutedRPC` **no tiene try/catch**: un fallo al deserializar los parametros escapa al loop de ZRpc. Un `try/catch` dentro del handler del mod no cubre la deserializacion.
- `ZNet.instance.GetPeer(uid)` en un cliente de **servidor dedicado** solo conoce al servidor. **No sirve** para validar el `sender` de otro jugador — los clientes no son peers entre si.
- `ZDOMan.GetSessionID()` es `public static`; un RPC dirigido a uno mismo llega con `sender == GetSessionID()`.
- No hay rate-limit de RPCs en ZRpc/ZNet/ZDOMan/sockets. El freno tiene que ponerlo el mod.

## Ship (verificado 2026-09-14)

- `Ship.CustomFixedUpdate`: el `if ((bool)m_nview && !m_nview.IsOwner()) return;` va **despues** de `UpdateControlls/UpdateSail/UpdateRudder`. Toda la fuerza se aplica dentro de la rama `if (!(num2 > m_disableLevel))` (o sea, solo en el agua).
- `Ship.m_backwardForce` (default 50) es la **unica** fuente de la fuerza de remo; solo se usa en `case Speed.Slow` y `case Speed.Back`. Con vela (Half/Full) no se usa.
- `Ship` no tiene `FixedUpdate`; lo llama `MonoUpdaters` recorriendo `Ship.Instances` secuencialmente ⇒ sin reentrada.
- `ShipControlls.m_nview = m_ship.GetComponent<ZNetView>()` ⇒ **mismo ZDO que el barco**. `GetUser()` lee `s_user` y lo puede leer cualquier cliente. Devuelve **playerID** (id de perfil), no peer id.
- `Ship.s_currentShips` se limpia en `OnTriggerExit` y en `OnDestroyed` (callback de WearNTear), **no** en OnDisable/OnDestroy ⇒ `Ship.GetLocalShip()` puede devolver un Ship destruido si ZNetScene lo saca por rango.
- `Ship.RefreshPlayerList()` filtra `m_players[i].GetOwner() == 0L` ⇒ vanilla asume que **un Player ZDO sin dueño es un estado que ocurre en la practica**.
- `Player.GetPlayerID()` devuelve `0L` si el nview no es valido; `Player.GetPlayer(0)` puede matchear a ese jugador.
