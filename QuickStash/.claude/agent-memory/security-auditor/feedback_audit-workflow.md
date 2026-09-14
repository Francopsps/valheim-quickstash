---
name: feedback-audit-workflow
description: Como correr auditorias de QuickStash — que no tocar, como verificar, que decisiones de diseno ya estan ratificadas
metadata:
  type: feedback
---

**No correr `QuickStash/build.ps1` sin permiso explicito en ese turno.**
**Why:** suele haber otro agente trabajando en paralelo sobre el mismo working tree; compilar
pisa `obj/` y `bin/` y le rompe el build al otro.
**How to apply:** si necesito confirmar que compila, pedirlo; si no, la verificacion es
estatica + decompilacion. Nunca afirmar que probe algo in-game: no hay Valheim en esta PC, las
pruebas las corre el humano en otra maquina.

**Decisiones de diseno ya ratificadas — no reportarlas como hallazgo.**
**Why:** el usuario ya las evaluo y las acepto con sus trade-offs; volver a levantarlas es ruido.
**How to apply:** al auditar, tratarlas como dadas y solo verificar que la implementacion
efectivamente las respeta:
- El auto-alimentado de hornos usa **solo cofres cuyo ZDO ya es nuestro** (nunca `ClaimOwnership`).
- Solo actua el cliente dueno del ZDO del horno (un cliente por horno, sin coordinacion).
- El mod **no parchea `Player.ConsumeResources`** (para no chocar con CraftFromContainers y otros).
- Mod 100% cliente: sin RPCs propios, sin ZDOs custom.

**El entregable es el mensaje final, no un .md.**
**Why:** el orquestador lee el texto de salida.
**How to apply:** reporte completo en el mensaje, con las 6 secciones (grado, top 3, invariantes
OK, riesgo residual, protocolo de prueba in-game).
