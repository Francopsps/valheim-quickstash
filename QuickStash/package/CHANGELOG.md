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
