# QuickStash

Mod cliente de [Valheim](https://www.valheimgame.com/) 1.0 (BepInEx 5 + HarmonyX) para
guardar rápido en cofres cercanos, marcar favoritos que nunca se guardan, y craftear usando el
material de los cofres de alrededor.

Escrito para un servidor de ~14 jugadores, así que el diseño prioriza no romper nada:
no agrega tráfico de red propio, no se instala en el servidor, y nunca escribe en un cofre que
otro jugador tenga abierto.

La documentación para jugadores está en [`QuickStash/package/README.md`](QuickStash/package/README.md).

## Estructura

```
QuickStash/
  src/          Código del mod
    Config/     Todas las opciones de configuración
    Core/       Registro de cofres, permisos, índice de materiales, log de movimientos
    Features/   Guardado rápido, favoritos, crafteo desde cofres
    UI/         Botón del inventario, Alt+clic, bordes de favoritos
  package/      Paquete estilo Thunderstore (manifest, icono, README, changelog)
  docs/
    api-1.0.7.md  Firmas de la API de Valheim verificadas por decompilación
    pruebas.md    Protocolo de prueba in-game
  agentes/      Definiciones de los agentes de revisión usados en el proyecto
  build.ps1     Compila y arma el zip
refs/Managed/   Assemblies del juego, compartidos entre mods (no se versionan)
```

## Compilar

Hacen falta dos cosas que no están en el repo:

1. **Los assemblies del juego.** Copiá `Valheim/valheim_Data/Managed` completa a `refs/Managed/`.
   Son archivos de Valheim: no se versionan ni se redistribuyen.
2. **El SDK de .NET 8.** No hace falta Visual Studio.

```powershell
.\QuickStash\build.ps1
```

Deja `QuickStash.dll` en `QuickStash/package/` y arma el zip de Thunderstore en la raíz del
proyecto. Para compilar con el juego en otra ruta:

```powershell
.\QuickStash\build.ps1 -ValheimManaged "D:\Steam\steamapps\common\Valheim\valheim_Data\Managed"
```

Y para que además copie el DLL a tu instalación y probar al toque:

```powershell
.\QuickStash\build.ps1 -ValheimPlugins "D:\Steam\steamapps\common\Valheim\BepInEx\plugins"
```

## Notas de implementación

Valheim 1.0 (9 de septiembre de 2026) cambió parte de su API interna, así que el mod se
construyó contra los assemblies reales decompilados con `ilspycmd`, no contra documentación.
Las firmas confirmadas están en [`QuickStash/docs/api-1.0.7.md`](QuickStash/docs/api-1.0.7.md); conviene revalidarlas
después de cada parche del juego.

Dos decisiones que explican buena parte del código:

- **Para tocar un cofre ajeno se usa el protocolo RPC del propio juego** (`RPC_RequestStack`),
  que le pide permiso al dueño del ZDO en vez de arrebatarle la propiedad. El dueño valida que
  nadie lo tenga abierto y que haya acceso.
- **El crafteo desde cofres no parchea `Player.ConsumeResources`**, que es el método que
  reemplazan otros mods del mismo tipo y la causa clásica de que los costos se descuenten dos
  veces. En vez de eso trae el material al inventario antes de que el juego craftee, y deja
  correr el flujo vanilla completo.

## Licencia

MIT.
