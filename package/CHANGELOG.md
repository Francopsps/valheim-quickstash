# Changelog

## 1.0.0
- Primera version.
- Boton de guardado rapido en el inventario, hacia los cofres cercanos.
- Favoritos por tipo de item y por casilla (Alt+clic), que nunca se guardan.
- Crafteo y mejoras en estaciones usando material de los cofres cercanos.

## 1.0.1
Correcciones de la auditoria previa al reparto. Nada de esto se probo in-game todavia.

- Se arreglo la reentrada del guardado rapido: cuando el cofre ya era propio, el RPC se
  resolvia en la misma pila y el resumen salia una vez por cofre.
- Nunca se escribe en un cofre sin ser dueno del ZDO. Sin propiedad el movimiento era local
  y el siguiente sync lo revertia, con los items ya descontados del inventario.
- Las unidades movidas se cuentan a mano en vez de con el delta de CountItems, que filtra por
  nivel de mundo: con nivel > 0 se podian perder items viejos.
- Las respuestas de apilado propias nunca caen al handler vanilla, que ignora los favoritos.
- El crafteo de recetas de un solo ingrediente ya no puede tirar NullReferenceException
  dentro del juego cuando el material no alcanza a llegar del cofre.
- Se piden permisos solo a los cofres que van a recibir algo (antes eran hasta 32 por pulsacion).
- Un solo guardado por cofre tambien al sacar material para craftear.
- Los parches de interfaz que corren por frame ya no pueden romper el inventario del jugador.
- Los favoritos se escriben de forma atomica y se reintentan si falla el disco.
- Se parchea por feature: si un parche del juego renombra un metodo, se apaga esa parte y no
  el mod entero.
- Cooldown de medio segundo por accion. Topes de configuracion mas conservadores.

## 1.0.2
- La tecla `N` guarda por defecto, sin abrir el inventario. No se dispara mientras escribis
  en el chat, la consola, un cartel o el comercio.
- El rango de guardado pasa de 10 a 30 metros.
- Nuevo `LogDeMovimientos`, activado por defecto: anota en el log cada objeto movido, con
  cantidad, cofre, posicion y hora. Es el rastro para investigar si alguna vez falta algo.
- Se corrigio la documentacion: ConfigurationManager (F1) es un mod aparte, no viene con
  BepInEx, y editar el .cfg a mano requiere reiniciar el juego.

## 1.0.3
- El rango de crafteo desde cofres tambien pasa a 30 metros, igual que el de guardado.

## 1.1.0
- **Arreglado: cofres que a veces no se tomaban.** No era la distancia. Se recortaba a los 32
  cofres mas cercanos ANTES de mirar cuales tenian el objeto, asi que con muchos cofres juntos
  los que quedaban fuera de ese tope no se usaban nunca. Ahora se filtra primero y el tope se
  aplica solo a los cofres a los que se les escribe. Leer un cofre no cuesta red.
- **Nuevo: construir desde cofres.** Con el martillo, las piezas usan el material de los cofres
  cercanos (30 m por defecto). El material se trae recien al colocar la pieza.
- **Nuevo: hornos automaticos.** Un cofre a menos de 10 m de una fundicion, horno de carbon,
  alto horno, molino, rueca o refineria de eitr la carga sola con combustible y mineral.
  Solo usa cofres que ya son tuyos, y solo actua el jugador mas cercano al horno.
- Con LogDeRendimiento activo, el resumen del guardado ahora dice cuantos cofres habia en rango,
  cuantos eran utilizables y cuantos tenian el objeto: si algo no se guarda, el log lo explica.
- Opcion nueva MaxCofresEscaneados (128) para el tope de lectura, separado de MaxCofresPorAccion
  (32) que sigue siendo el de escritura.
- Los hornos respetan ItemsExcluidos, tienen tope global por segundo, y sus cargas no llenan el
  log salvo que actives Hornos > LogDeCarga.

## 1.2.0 (experimental — rama feature/animales-comen-de-cofres)
- Los animales domesticados comen de un cofre cercano cuando tienen hambre y no hay nada en el
  piso. Solo agarran lo que ese animal come normalmente: el filtro es la lista del propio juego.
- La comida se tira al lado del animal, no del cofre, asi que el cofre puede estar fuera del cerco.
- Solo actua el cliente dueno del animal y solo usa cofres propios, igual que los hornos.

## 1.2.1 (experimental)
- Diagnostico para los animales: con LogDeRendimiento activo, el log explica por que un animal
  hambriento no comio (no hay cofre, el cofre es de otro jugador, o la comida no le gusta) y
  vuelca una vez por especie la lista real de lo que come.

## 1.3.0
- **Arreglado: los animales no comian en el servidor.** El enganche anterior colgaba de la IA del
  animal, que Valheim solo ejecuta en el cliente dueno de su ZDO. Con varios jugadores, el que
  esta parado al lado del corral tipicamente NO es el dueno, asi que el codigo no corria nunca.
- Rediseno: ya no hace falta ser dueno del animal. El mod solo deja la comida en el piso y el
  cliente que si es dueno se la come con codigo vanilla. Efecto lateral: alcanza con que UNO de
  los jugadores tenga el mod para que se alimenten los animales de toda la base.
- Coordinacion entre clientes: solo actua el jugador mas cercano al animal, y no se tira comida
  si ya hay en el piso.
- Opciones nuevas: Animales > IntervaloSegundos y Animales > MaxPorCiclo.

## 1.3.1
Correcciones de la auditoria de seguridad sobre master.

- **Critico: los animales podian vaciar un cofre sin freno.** La unica defensa contra tirar
  comida en cada ciclo era un barrido de fisica acotado a 32 objetos. En un corral con basura
  tirada —tipico despues de un raid, y dentro de una base lo tirado no despawnea— ese barrido se
  truncaba, no veia la comida que ya estaba en el piso, y seguia sacando del cofre
  indefinidamente. Ahora el buffer es de 256 y, si aun asi se satura, se asume que hay comida en
  vez de lo contrario.
- Cooldown de 10 s por animal tras darle de comer, para que el freno no dependa de un solo
  chequeo.
- La eleccion de que jugador alimenta ya no depende solo de la distancia: con posiciones
  replicadas y con lag, dos jugadores casi equidistantes podian creerse ambos el mas cercano.
- No se le tira comida a un animal muerto.
- LogDeComida pasa a estar activado por defecto: es la funcion que mas necesita dejar rastro.
- Lista de exclusion propia para animales, separada de la global.
- Los ciclos de animales corren aislados: una excepcion ahi ya no mata el atajo de guardado.
- El diagnostico deja de repetir "no hay animales" cada 5 segundos.

## 1.3.2
- El rango de los hornos pasa de 10 a 15 metros.

## 1.3.3
- El rango del guardado rapido baja de 30 a 15 metros.

