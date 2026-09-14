# QuickStash

Mod de cliente para Valheim 1.0. Hace seis cosas:

1. **Un botón en el inventario** que manda de golpe a los cofres cercanos los objetos que
   esos cofres ya contienen.
2. **Favoritos**: marcás ítems o casillas con `Alt+clic` y el botón nunca los guarda.
3. **Craftear desde cofres**: estando en la forja o en una mesa de crafteo, las recetas usan
   el material de los cofres de alrededor sin que lo tengas que sacar a mano.
4. **Construir desde cofres**: con el martillo, las piezas usan el material de los cofres
   cercanos.
5. **Hornos automáticos**: un cofre al lado de una fundición o un horno de carbón lo carga solo
   con combustible y mineral.
6. **Animales que comen del cofre**: los domesticados sacan comida de un cofre cercano cuando
   tienen hambre. *(Experimental, rama `feature/animales-comen-de-cofres`.)*

Está pensado para un servidor con mucha gente: no agrega tráfico de red propio, no toca el
servidor y nunca escribe en un cofre que otro jugador tenga abierto.

## Instalación

Es **solo cliente**: lo instala cada jugador que lo quiera usar, y no hace falta ponerlo en
el servidor. Se puede jugar con gente que no lo tenga.

1. Tener `BepInExPack_Valheim` (5.4.2350 o superior).
2. Copiar `QuickStash.dll` a `BepInEx\plugins\`.

## Uso

### Guardar rápido

Abrí el inventario y apretá el botón **Guardar** (arriba a la derecha del panel).

- Solo manda cosas a los cofres que **ya tienen ese tipo de objeto**. Un cofre vacío no se
  llena solo, igual que el botón de apilar del juego.
- La primera fila del inventario (la barra rápida) queda protegida por defecto.
- Lo que tengas equipado nunca se guarda.
- Si hay un cofre abierto, se usa primero.
- También se puede guardar con la tecla **`N`**, sin abrir el inventario. No se dispara
  mientras estás escribiendo en el chat, la consola o un cartel. Se cambia en `Atajo`.

### Favoritos

- `Alt` + clic izquierdo **sobre un ítem** → protege ese **tipo de ítem**. Toda tu madera
  queda protegida, esté en la casilla que esté. Borde amarillo.
- `Alt` + clic izquierdo **sobre una casilla vacía** → protege esa **casilla**, que no se va a
  usar para nada. Borde celeste.
- `Alt` + `Shift` + clic sobre un ítem → protege la **casilla** en vez del tipo.
- Volvé a hacer el mismo clic para desmarcar.

Los favoritos se guardan por personaje, en
`BepInEx\config\QuickStash\favorites.<personaje>.json`.

### Construir desde cofres

Con el martillo en la mano, las piezas cuentan también el material de los cofres cercanos
(30 m por defecto). El material sale del cofre recién cuando colocás la pieza, no antes.

### Hornos automáticos

Poné un cofre a menos de 10 m de una **fundición, horno de carbón, alto horno, molino, rueca o
refinería de eitr** y se carga solo con lo que corresponda: carbón y mineral para la fundición,
madera para el horno de carbón, y así.

**Cuidado con el horno de carbón.** Acepta *varios* tipos de madera y todos le dan carbón por
igual, así que un cofre con madera fina a 10 m se te puede convertir solo. Es irreversible. Si
guardás madera valiosa cerca de un horno de carbón, ponela en `ItemsExcluidos` (por ejemplo
`FineWood,RoundLog,ElderBark`) o alejá el cofre.

Otras dos cosas que conviene saber:

- **Solo usa cofres que ya son tuyos.** En una base compartida, si otro jugador es dueño del
  cofre (Valheim le asigna la propiedad al que llegó primero y sigue cerca), el horno no lo va a
  tocar hasta que la propiedad pase a vos. Es a propósito: evita cualquier riesgo de duplicar
  objetos en un ciclo que corre solo.
- **Solo actúa el jugador más cercano al horno**, así que aunque los 14 tengan el mod, un horno
  lo alimenta un solo cliente.

### Animales que comen del cofre *(experimental)*

Poné un cofre a menos de 10 m de tus jabalíes, lobos o lo que tengas domesticado. Cuando les da
hambre y no hay comida en el piso, sacan una unidad del cofre y se la comen.

- **Solo agarran lo que ese animal come normalmente.** El filtro es la lista del propio juego
  (`m_consumeItems`), no una lista nuestra: un jabalí no va a comerse tu carne de lobo.
- La comida cae **al lado del animal**, no del cofre, así que el cofre puede estar del otro lado
  del cerco sin problema.
- Todo lo demás lo hace el juego: la animación, el hambre, y el progreso de domesticación o de
  cría. Para el juego es exactamente como si se la hubieras tirado vos.
- **Con que uno solo de ustedes tenga el mod, alcanza.** El mod deja la comida en el piso y
  después come el animal por su cuenta, con código del juego base: los animales se alimentan
  aunque el dueño del corral no tenga QuickStash instalado.
- **Ojo con la cría**: si les dejás comida infinita al lado, se van a reproducir sin parar. Si no
  querés eso, sacá el cofre o usá `Animales > Activado = false`.

### Craftear desde cofres

Parado en una estación (forja, mesa de trabajo, etc.), las recetas cuentan también lo que hay
en los cofres cercanos. El número del requisito se pinta en celeste cuando alcanza gracias a
los cofres, y el material se trae solo al momento de craftear.

Fuera de una estación el mod no interviene: craftear a mano y construir con el martillo
funcionan exactamente como en el juego base.

## Configuración

El archivo es `BepInEx\config\com.valheimcrew.quickstash.cfg`. Aparece recién después de entrar
al juego una vez con el mod instalado.

Hay dos formas de tocarlo:

- **A mano**, con un editor de texto, con el juego cerrado. BepInEx no vigila el archivo, así
  que hay que **reiniciar el juego** para que tome los cambios.
- **In-game con `F1`**, que es más cómodo y aplica al instante. Para eso hace falta instalar
  aparte el mod [Official BepInEx ConfigurationManager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/)
  — **no viene incluido en BepInEx**. Es solo cliente: lo instala quien lo quiera.

Lo que probablemente quieras tocar:

| Opción | Por defecto | Para qué |
|---|---|---|
| `Rango` | 30 | Metros a la redonda para buscar cofres al guardar |
| `Atajo` | `N` | Tecla para guardar sin abrir el inventario |
| `ProtegerBarraRapida` | true | No guarda la primera fila del inventario |
| `PosicionX` / `PosicionY` | 0 | Mover el botón si te queda mal ubicado o chocado con otro mod de UI |
| `ItemsExcluidos` | vacío | Prefabs que nunca se guardan **ni se cargan a un horno**, separados por coma |
| `Crafteo > Rango` | 30 | Metros para buscar material al craftear |
| `Construccion > Rango` | 30 | Metros para buscar material al construir |
| `Hornos > Rango` | 10 | Metros entre el horno y el cofre |
| `Hornos > IntervaloSegundos` | 2 | Cada cuánto revisa cada horno |
| `Hornos > MaxPorCiclo` | 5 | Cuántas unidades carga por horno en cada revisión |
| `Hornos > MaxHornosPorSegundo` | 6 | Tope global: cuántos hornos distintos atiende por segundo |
| `Hornos > LogDeCarga` | false | Anotar también las cargas automáticas en el log |
| `Animales > Activado` | true | Los domesticados comen del cofre cercano |
| `Animales > Rango` | 10 | Metros entre el animal y el cofre |
| `Animales > IntervaloSegundos` | 5 | Cada cuánto se revisan los animales cercanos |
| `Animales > MaxPorCiclo` | 3 | A cuántos animales se les deja comida por revisión |
| `Animales > LogDeComida` | false | Anotar en el log cada vez que un animal saca comida |
| `MaxCofresPorAccion` | 32 | Tope de cofres por pulsación |
| `LogDeMovimientos` | **true** | Anota en el log cada objeto que se mueve, con cofre, posición y hora |
| `LogDeRendimiento` | false | Escribe en el log los ms y la cantidad de cofres de cada acción |

## Multijugador

- Un cofre que otro jugador tiene abierto **se salta**, no se toca. Vas a ver el aviso
  "cofre en uso".
- Para escribir en un cofre ajeno se usa el mismo pedido de permiso que el botón de apilar
  del juego: el dueño del cofre valida y recién ahí cede el control.
- Se respetan los cofres privados y los wards, con las mismas reglas que el juego.

## Limitaciones conocidas

- Si el inventario está lleno, craftear desde cofres puede fallar con el mensaje de "sin
  espacio" del juego: el material se trae al inventario antes de consumirse y necesita un
  hueco. No se pierde nada.
- La posición por defecto del botón puede no ser perfecta según la resolución o si usás otros
  mods de interfaz. Se ajusta con `PosicionX` y `PosicionY`.
- No conviene usarlo junto a QuickStackStore ni a CraftFromContainers: hacen lo mismo y se
  pisan. El mod avisa en el log si detecta alguno.

## Registro de movimientos

Por defecto el mod anota en `BepInEx/LogOutput.log` todo lo que mueve, una línea por cofre:

```
[Info   :   QuickStash] GUARDADO -> Cofre (1243, 31, -678) | 21:15:03 | Madera x40, Piedra x12
[Info   :   QuickStash] SACADO <- Cofre de hierro (1250, 31, -670) | 21:16:44 | Hierro x10
```

Sirve para investigar si alguna vez falta algo: buscás el nombre del objeto en el log y ves
cuándo se movió, cuánto y a qué cofre (la posición está para poder ir a buscarlo al mundo).
Se apaga con `LogDeMovimientos = false`, pero conviene dejarlo: es el único rastro que queda.

Las cargas automáticas de horno **no** se anotan por defecto: son un goteo continuo que taparía
todo lo demás. Si las querés, activá `Hornos > LogDeCarga` y aparecen con la etiqueta
`HORNO <-`, para poder separarlas con un buscador.

