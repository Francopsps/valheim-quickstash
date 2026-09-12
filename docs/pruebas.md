# Protocolo de prueba in-game — QuickStash

Nada de esto se puede verificar desde el PC de desarrollo: no hay Valheim instalado. Todo lo
que sigue lo corre una persona en el PC de juego.

**Antes de empezar**

1. `LogDeRendimiento = true` en `BepInEx\config\com.valheimcrew.quickstash.cfg`.
2. Backup de la carpeta del mundo del servidor.
3. Primera pasada en un **mundo de prueba en solitario, solo con BepInEx + QuickStash**. Recién
   después, el servidor.

---

## A. Humo — que cargue

1. Entrar al juego y buscar en `BepInEx\LogOutput.log` la línea `QuickStash 1.0.1 cargado`.
2. **No debe haber** líneas `No se pudo enganchar '<feature>'`. Si aparece alguna, el juego
   cambió ese método: anotar cuál y revalidar contra `docs/api-1.0.7.md`.
3. **No debe haber** excepciones de Harmony.

---

## B. Guardado rápido

**B1 — comportamiento base.** Tres cofres con madera y uno vacío a menos de 10 m. Con madera en
el inventario, apretar **Guardar**.
- La madera va solo a los cofres que ya tenían madera; el vacío queda igual.
- Aparece **un solo** mensaje central y **un solo** efecto de movimiento.

**B2 — reentrada (era el bug más común).** Pararse rodeado de 8 o más cofres **propios** (los
que abriste vos hace poco). Apretar Guardar una vez.
- **Correcto:** un mensaje, un efecto, una sola línea `[stash]` en el log.
- **Falla:** cascada de mensajes con contadores crecientes y varias líneas `[stash]`.

**B3 — persistencia.** Después de B1, alejarse 200 m hasta que se descargue la zona, volver y
abrir los cofres.
- **Falla:** la madera no está ni en el cofre ni en el inventario. Contar antes y después.

**B4 — cofre ajeno abierto.** Dos jugadores. A abre un cofre; B, a menos de 10 m, aprieta
Guardar.
- Ese cofre se salta. B ve `Sin guardar: N cofre(s) en uso por otro jugador` o el resto se
  guarda normalmente.
- Contar el contenido del cofre antes y después: no debe cambiar por acción de B.

**B5 — tráfico.** Con el log activo, apretar Guardar con el inventario **sin nada apilable**.
- **Correcto:** `[stash] 0 items -> 0 cofres` y **ninguna** línea `wants to stack all` en el log.
- **Falla:** decenas de `wants to stack all`. Significa que el filtro previo no está actuando.

---

## C. Favoritos

**C1 — marcar.** Alt+clic sobre un ítem → borde amarillo. Alt+clic sobre casilla vacía → borde
celeste. Alt+Shift+clic sobre un ítem → borde celeste (protege la casilla).

**C2 — que no se guarden.** Con un favorito cuyo tipo **también exista** en un cofre cercano,
apretar Guardar. El favorito se queda. Lo mismo con la barra rápida y `ProtegerBarraRapida = true`.

**C3 — el caso que se rompía.** Apretar Guardar **dos veces seguidas, rápido**, con 6-8 cofres
cercanos, al menos uno de otro jugador.
- **Falla:** el favorito termina en un cofre, o la barra rápida se vacía. Eso significa que una
  respuesta quedó huérfana y la atendió el apilado vanilla, que no conoce favoritos.

**C4 — persistencia.** Marcar 5 favoritos, esperar 3 segundos, matar el juego desde el
Administrador de tareas, volver a entrar. Los 5 siguen.

**C5 — disco de solo lectura.** Poner `BepInEx\config\QuickStash\favorites.<personaje>.json` en
solo lectura y marcar un favorito. No debe crashear: solo un warning en el log.

---

## D. Crafteo desde cofres

**D1 — base.** Forja con hierro en un cofre al lado. La receta aparece disponible y el número
del requisito sale en celeste. Craftear descuenta del cofre.

**D2 — límite de rango.** Alejarse más de `Crafteo > Rango`: la receta vuelve a aparecer como no
disponible.

**D3 — sin estación.** Craftear a mano, lejos de todo cofre: comportamiento idéntico al juego
base, no toma nada de cofres.

**D4 — receta de un solo ingrediente.** Buscar una receta que acepte varios materiales
alternativos, con el material solo en un cofre. Pedirle a otro jugador que vacíe ese cofre justo
antes de apretar Craftear.
- **Correcto:** mensaje de material faltante y nada se rompe.
- **Falla:** `NullReferenceException` en `Recipe.GetAmount` en el log y el botón queda muerto.

**D5 — inventario lleno.** Llenar el inventario, seleccionar una receta cuyo producto no entre,
con el material solo en cofres. Apretar Craftear.
- **Correcto:** el juego avisa que no hay espacio y **los cofres quedan intactos**.
- **Falla:** el material salió de los cofres y no se crafteó nada.

**D6 — un guardado por cofre.** Con el log activo, craftear algo que consuma material de 2-3
cofres. Revisar que no haya una avalancha de escrituras.

---

## E. Regresión — que no se rompa nada vanilla

1. Abrir un cofre y usar el botón **de apilar del juego**: debe funcionar igual que siempre.
2. Shift+clic (partir stack) y Ctrl+clic (mover) en el inventario: siguen funcionando.
3. Craftear y mejorar con material **solo en el inventario**, lejos de cofres: el costo se
   descuenta una sola vez (confirma que no hay choque con otros mods).
4. Un jugador **sin** el mod ve los mismos contenidos de cofre que uno con el mod.
5. Cofres privados y cofres dentro de un ward ajeno: se saltan.

---

## F. Riesgo residual conocido — no es un bug a reportar

**Robo de propiedad al sacar material para craftear.** El crafteo necesita sacar material de
forma síncrona, así que reclama la propiedad del ZDO del cofre. Si otro jugador abrió ese cofre
en los milisegundos anteriores y el aviso de "en uso" todavía no llegó por la red, se le puede
quitar la propiedad; a partir de ahí lo que ese jugador mueva en ese cofre no se guarda.

Está mitigado: se usan primero los cofres que ya son propios, se revalida el estado "en uso"
justo antes de escribir, y no se toca ningún cofre marcado en uso. La ventana que queda es de
fracciones de segundo.

**Cómo se ve si pasa:** al jugador afectado le queda el cofre visualmente abierto (tapa
levantada) y deja de actualizarse en su cliente. Si alguien reporta eso, anotar quién estaba
crafteando cerca y avisar — se puede cerrar del todo bajando `Crafteo > Rango`, o desactivando
`Crafteo > Activado` si molesta.

---

## G. Ajuste del botón

La posición por defecto del botón (arriba a la derecha del panel del inventario) se eligió sin
poder verla. Si queda mal ubicado o choca con otro mod de interfaz, moverlo con `PosicionX` y
`PosicionY` desde Configuration Manager (F1), sin reiniciar. Anotar los valores que queden bien
para ponerlos como default.
