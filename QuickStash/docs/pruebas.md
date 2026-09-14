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

1. Entrar al juego y buscar en `BepInEx\LogOutput.log` la línea `QuickStash <version> cargado` con la version que acabas de instalar.
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

---

# Pruebas de la 1.1.0

## H. Cofres apilados (el bug que se arregló)

**H1 — el caso que fallaba.** Armar 40 o más cofres dentro de 30 m (apilados sirve), poner el
**único** cofre con hierro entre los más lejanos del grupo, y guardar hierro desde el inventario.
- **Correcto:** el hierro entra en ese cofre.
- Con `LogDeRendimiento = true`, la línea `[stash]` tiene que mostrar `en rango` mayor a 32 y
  `pedidos` menor o igual a 32. Eso confirma que ahora se leen todos y se le escribe solo a los
  que hacen falta.

**H2 — el tope de escritura sigue vivo.** Con 40+ cofres que **todos** tengan madera, guardar
madera. `pedidos` no puede pasar de 32. Si pasa, el tope de red dejó de funcionar.

## I. Construir desde cofres

**I1 — base.** Inventario sin madera ni piedra, un cofre con las dos a menos de 30 m. Sacar el
martillo: la pieza tiene que aparecer construible (fantasma en azul, no rojo). Colocarla descuenta
del cofre. Verificar la línea `SACADO <-` en el log.

**I2 — límite de rango.** Alejarse más de `Construccion > Rango` del cofre: la pieza vuelve a
aparecer como no construible.

**I3 — no se construye gratis.** Sin material ni en el inventario ni en ningún cofre, la pieza no
se coloca. **Contar** el material antes y después de construir 5 piezas: el descuento total tiene
que ser exactamente el costo de las 5.

**I4 — colocación fallida.** Apuntar a un lugar inválido (dentro de la roca, fuera de la zona de
construcción) e intentar colocar. **Falla si:** el material salió del cofre igual. No debería
moverse nada.

**I5 — inventario lleno.** Con el inventario lleno y el material solo en cofres, intentar
construir. No debe romperse nada ni construirse gratis.

## J. Hornos automáticos

**J1 — fundición.** Cofre con carbón y mineral de cobre a menos de 10 m de una fundición.
- Se carga sola en pocos segundos.
- En el log aparecen líneas `SACADO <-` con el cofre y la cantidad.

**J2 — horno de carbón.** Cofre con madera al lado. Ojo: ahí la madera entra como **mineral**, no
como combustible (el horno de carbón no tiene `m_fuelItem`).

**J3 — se detiene al llenarse.** Dejar el horno cargado al tope y mirar el cofre: no se le puede
seguir sacando nada. **Falla si:** el cofre sigue vaciándose.

**J4 — respeta el radio.** Mover el cofre a 15 m del horno: deja de cargarse.

**J5 — no toca lo que no corresponde.** Poner en el mismo cofre objetos que ese horno no acepta
(por ejemplo comida en una fundición). No se los tiene que llevar.

**J6 — apagado.** Con `Hornos > Activado = false`, nada se mueve solo.

**J7 — cuentas, lo más importante.** Contar exactamente cuánto mineral hay en el cofre, esperar a
que el horno lo consuma todo, y contar cuántas barras salieron. Las cuentas tienen que cerrar: ni
material que desaparece ni barras de más.

## K. Hornos en el servidor (dos jugadores)

**K1 — un solo alimentador.** Los dos jugadores con el mod, parados cerca del mismo horno con un
cofre al lado. Contar el cofre antes y después de un rato. **Falla si:** el consumo del cofre es
mayor que lo que entró al horno (eso sería doble alimentación).

**K2 — cofre ajeno.** Que el otro jugador sea el dueño del cofre pegado a tu horno (alcanza con
que lo haya abierto él último). **Comportamiento esperado: el horno NO se alimenta.** Es la
decisión de diseño, no un bug. Si molesta en la práctica, avisar y se revisa.

**K3 — cofre abierto.** Mientras el otro jugador tiene el cofre abierto, el horno no lo toca.

## L. Construir a costo parcial (lo que encontró la auditoría)

Era el agujero más serio de la 1.1.0: se podía colocar una pieza pagando **solo una parte** del
costo, porque `Inventory.RemoveItem` descuenta lo que encuentra y sigue en silencio.

**L1 — el caso determinista.**
1. Dos cofres al lado de una mesa de trabajo: uno con 50 madera fina, otro con 20 cuero de ciervo.
   Cero de los dos en el inventario.
2. Llenar el inventario con basura hasta dejar **exactamente un casillero libre**. Contarlo.
3. Seleccionar la **cama** (8 madera fina + 4 cuero de ciervo). Anotar el contenido de los dos
   cofres.
4. Colocarla.
5. **Correcto:** o no se coloca y sale el mensaje de material faltante, o se coloca y se
   descuentan los dos materiales completos.
6. **Falla:** la cama queda construida, salieron 8 madera fina y el cuero **no se movió**.

**L2 — carrera del caché.** Dos jugadores. A con el martillo apuntando a un lugar válido, junto a
un cofre con exactamente 10 madera. B parado en ese cofre. A la cuenta de tres, B saca las 10 y A
coloca. Repetir unas 15 veces: ninguna colocación puede salir sin descontar.

**L3 — construcción gratis por global key.** Con `nocost` activado por admin, construir al lado de
un cofre lleno. **Falla si:** el material sale del cofre igual (la pieza es gratis, no debería
moverse nada).

## M. Hornos: lo que agregó la auditoría

**M1 — conversión no deseada.** Horno de carbón con un cofre al lado que tenga **un stack de cada
tipo de madera** (normal, fina, de núcleo, corteza de anciano). Esperar 3-4 ciclos y **anotar
cuáles desaparecieron**. Eso define qué acepta el horno, que es el dato que no se puede sacar del
código. Con esa lista se decide qué poner en `ItemsExcluidos`.

**M2 — exclusiones.** Poner `ItemsExcluidos = FineWood` y repetir M1: la madera fina no se toca.

**M3 — tope global.** 20 hornos con cofres al lado y `MaxHornosPorSegundo = 6`. El consumo total
tiene que ser notoriamente más lento que con el tope en 32. Confirma que la cota actúa.

**M4 — no sobrecarga.** Fundición vacía, cofre al lado con **dos stacks separados** de carbón y
dos de mineral, `MaxPorCiclo = 20`. El combustible no puede pasar del máximo del horno ni la cola
de 10.

**M5 — cuentas cerradas.** Cofre con exactamente 100 madera junto a un horno de carbón. Dejarlo
correr 5 minutos con otro jugador moviéndose cerca. Al final: madera en el cofre + madera en la
cola + carbón producido tiene que cerrar en 100. Revisar el log por cualquier
`Fallo al cargar el horno desde los cofres:` — si aparece aunque sea una vez, avisar.

**M6 — tope de escritura del guardado.** Con `MaxCofresEscaneados = 128` y
`MaxCofresPorAccion = 4`, rodearse de 20 cofres que todos tengan madera y guardar. El log tiene
que decir `con coincidencia 20 | pedidos 4 (tope 4)`.

## N. Animales que comen del cofre

El primer intento fallo en el servidor porque el codigo colgaba de la IA del animal, que solo
corre en el cliente dueno de su ZDO. Estas pruebas van **en el servidor**, no en solitario: en
solitario sos dueno de todo y el bug original no se reproduce.

**N1 — el caso que fallaba.** Corral con jabalies hambrientos y un cofre TUYO con zanahorias a
menos de 10 m. Con `LogDeRendimiento = true`, mirar el log.
- **Correcto:** comen, aunque el barrido siga diciendo `dueno del ZDO: NO`. Esa combinacion es
  justamente la prueba de que el rediseno funciona.
- Contar el cofre antes y despues: tiene que bajar exactamente lo que comieron.

**N2 — solo lo que comen.** Carne cruda en el mismo cofre: no la tocan.

**N3 — no se acumula comida.** Mirar el piso un rato: nunca mas de una unidad tirada por animal.

**N4 — cofre fuera del cerco.** A menos de 10 m pero del otro lado de la cerca: funciona igual.

**N5 — rango.** Cofre a 15 m: dejan de comer.

**N6 — dos jugadores con el mod.** Los dos parados en el corral. Contar el cofre: si baja el
doble de lo que comieron, la eleccion por cercania no esta actuando.

**N7 — un jugador sin el mod.** Que el dueno del corral NO tenga QuickStash y vos si. Los animales
tienen que comer igual. Es la propiedad nueva del rediseno.

**N8 — cuentas cerradas.** Cofre con exactamente 50 zanahorias. Dejarlo 30 minutos. Zanahorias
que faltan = veces que comieron. Revisar el log por `Fallo al dar de comer desde el cofre:`.

**N9 — cria.** Con comida disponible se reproducen normalmente. Es lo esperado, pero conviene
medir a que ritmo antes de dejarlo fijo en el servidor.

**N10 — el crítico de la auditoría: goteo sin freno.** Es la prueba más importante de esta versión.
1. Corral con 2-3 jabalíes hambrientos **dentro de la base**, cofre propio al lado con 500 de comida.
2. Tirar al piso **más de 40 objetos NO comestibles** (madera, piedra, plumas) en 5 m alrededor de
   los animales. Dentro de una base eso no despawnea nunca.
3. Encerrarlos para que no lleguen a la comida, o esperar un raid (durante un raid no comen).
4. `LogDeComida = true`. Dejar 15 minutos.
5. **Falla si:** aparecen más de un par de líneas `ANIMAL <-` en esos 15 minutos, o si al bajar al
   corral hay una pila creciente de comida. Con el arreglo tienen que ser 0 mientras haya comida
   en el piso.

**N11 — doble tiro entre clientes.** Dos jugadores con el mod, quietos, a la misma distancia
(~8 m) a cada lado del mismo animal hambriento, cada uno con su cofre. Ambos con `LogDeComida`.
Esperar 5 minutos. **Falla si:** los dos logs muestran líneas `ANIMAL <-` para el mismo animal de
forma repetida. Repetir caminando en círculo alrededor del animal, que es donde más pega el lag.

**N12 — aislamiento.** Si aparece cualquier `Fallo en alimentacion de animales` en el log, probar
enseguida la tecla `N`. Tiene que seguir funcionando.

