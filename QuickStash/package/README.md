# QuickStash

Mod de cliente para Valheim 1.0. Hace tres cosas:

1. **Un botÃ³n en el inventario** que manda de golpe a los cofres cercanos los objetos que
   esos cofres ya contienen.
2. **Favoritos**: marcÃ¡s Ã­tems o casillas con `Alt+clic` y el botÃ³n nunca los guarda.
3. **Craftear desde cofres**: estando en la forja o en una mesa de crafteo, las recetas usan
   el material de los cofres de alrededor sin que lo tengas que sacar a mano.

EstÃ¡ pensado para un servidor con mucha gente: no agrega trÃ¡fico de red propio, no toca el
servidor y nunca escribe en un cofre que otro jugador tenga abierto.

## InstalaciÃ³n

Es **solo cliente**: lo instala cada jugador que lo quiera usar, y no hace falta ponerlo en
el servidor. Se puede jugar con gente que no lo tenga.

1. Tener `BepInExPack_Valheim` (5.4.2350 o superior).
2. Copiar `QuickStash.dll` a `BepInEx\plugins\`.

## Uso

### Guardar rÃ¡pido

AbrÃ­ el inventario y apretÃ¡ el botÃ³n **Guardar** (arriba a la derecha del panel).

- Solo manda cosas a los cofres que **ya tienen ese tipo de objeto**. Un cofre vacÃ­o no se
  llena solo, igual que el botÃ³n de apilar del juego.
- La primera fila del inventario (la barra rÃ¡pida) queda protegida por defecto.
- Lo que tengas equipado nunca se guarda.
- Si hay un cofre abierto, se usa primero.
- TambiÃ©n se puede guardar con la tecla **`N`**, sin abrir el inventario. No se dispara
  mientras estÃ¡s escribiendo en el chat, la consola o un cartel. Se cambia en `Atajo`.

### Favoritos

- `Alt` + clic izquierdo **sobre un Ã­tem** â†’ protege ese **tipo de Ã­tem**. Toda tu madera
  queda protegida, estÃ© en la casilla que estÃ©. Borde amarillo.
- `Alt` + clic izquierdo **sobre una casilla vacÃ­a** â†’ protege esa **casilla**, que no se va a
  usar para nada. Borde celeste.
- `Alt` + `Shift` + clic sobre un Ã­tem â†’ protege la **casilla** en vez del tipo.
- VolvÃ© a hacer el mismo clic para desmarcar.

Los favoritos se guardan por personaje, en
`BepInEx\config\QuickStash\favorites.<personaje>.json`.

### Craftear desde cofres

Parado en una estaciÃ³n (forja, mesa de trabajo, etc.), las recetas cuentan tambiÃ©n lo que hay
en los cofres cercanos. El nÃºmero del requisito se pinta en celeste cuando alcanza gracias a
los cofres, y el material se trae solo al momento de craftear.

Fuera de una estaciÃ³n el mod no interviene: craftear a mano y construir con el martillo
funcionan exactamente como en el juego base.

## ConfiguraciÃ³n

El archivo es `BepInEx\config\com.valheimcrew.quickstash.cfg`. Aparece reciÃ©n despuÃ©s de entrar
al juego una vez con el mod instalado.

Hay dos formas de tocarlo:

- **A mano**, con un editor de texto, con el juego cerrado. BepInEx no vigila el archivo, asÃ­
  que hay que **reiniciar el juego** para que tome los cambios.
- **In-game con `F1`**, que es mÃ¡s cÃ³modo y aplica al instante. Para eso hace falta instalar
  aparte el mod [Official BepInEx ConfigurationManager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/)
  â€” **no viene incluido en BepInEx**. Es solo cliente: lo instala quien lo quiera.

Lo que probablemente quieras tocar:

| OpciÃ³n | Por defecto | Para quÃ© |
|---|---|---|
| `Rango` | 30 | Metros a la redonda para buscar cofres al guardar |
| `Atajo` | `N` | Tecla para guardar sin abrir el inventario |
| `ProtegerBarraRapida` | true | No guarda la primera fila del inventario |
| `PosicionX` / `PosicionY` | 0 | Mover el botÃ³n si te queda mal ubicado o chocado con otro mod de UI |
| `ItemsExcluidos` | vacÃ­o | Prefabs que nunca se guardan, separados por coma |
| `Crafteo > Rango` | 30 | Metros para buscar material al craftear |
| `MaxCofresPorAccion` | 32 | Tope de cofres por pulsaciÃ³n |
| `LogDeMovimientos` | **true** | Anota en el log cada objeto que se mueve, con cofre, posiciÃ³n y hora |
| `LogDeRendimiento` | false | Escribe en el log los ms y la cantidad de cofres de cada acciÃ³n |

## Multijugador

- Un cofre que otro jugador tiene abierto **se salta**, no se toca. Vas a ver el aviso
  "cofre en uso".
- Para escribir en un cofre ajeno se usa el mismo pedido de permiso que el botÃ³n de apilar
  del juego: el dueÃ±o del cofre valida y reciÃ©n ahÃ­ cede el control.
- Se respetan los cofres privados y los wards, con las mismas reglas que el juego.

## Limitaciones conocidas

- Si el inventario estÃ¡ lleno, craftear desde cofres puede fallar con el mensaje de "sin
  espacio" del juego: el material se trae al inventario antes de consumirse y necesita un
  hueco. No se pierde nada.
- La posiciÃ³n por defecto del botÃ³n puede no ser perfecta segÃºn la resoluciÃ³n o si usÃ¡s otros
  mods de interfaz. Se ajusta con `PosicionX` y `PosicionY`.
- No conviene usarlo junto a QuickStackStore ni a CraftFromContainers: hacen lo mismo y se
  pisan. El mod avisa en el log si detecta alguno.

## Registro de movimientos

Por defecto el mod anota en `BepInEx/LogOutput.log` todo lo que mueve, una lÃ­nea por cofre:

```
[Info   :   QuickStash] GUARDADO -> Cofre (1243, 31, -678) | 21:15:03 | Madera x40, Piedra x12
[Info   :   QuickStash] SACADO <- Cofre de hierro (1250, 31, -670) | 21:16:44 | Hierro x10
```

Sirve para investigar si alguna vez falta algo: buscÃ¡s el nombre del objeto en el log y ves
cuÃ¡ndo se moviÃ³, cuÃ¡nto y a quÃ© cofre (la posiciÃ³n estÃ¡ para poder ir a buscarlo al mundo).
Se apaga con `LogDeMovimientos = false`, pero conviene dejarlo: es el Ãºnico rastro que queda.

