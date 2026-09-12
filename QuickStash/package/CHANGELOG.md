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
