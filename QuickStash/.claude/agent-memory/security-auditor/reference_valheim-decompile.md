---
name: reference-valheim-decompile
description: Como decompilar assembly_valheim.dll con ilspycmd y que cubre (y que NO cubre) docs/api-1.0.7.md
metadata:
  type: reference
---

Fuente de verdad de la API del juego: decompilar. No hay Valheim instalado en esta PC.

```
cd "J:/Mod Valheim/refs/Managed"
PATH="/c/Program Files/dotnet:$PATH:$HOME/.dotnet/tools" ilspycmd assembly_valheim.dll -t Smelter
```

- Un `-t Tipo` tarda ~20-60 s. Sin `-t` vuelca el assembly entero (util solo para buscar
  call sites con grep, p. ej. `| grep -n "TryPlacePiece"`).
- `docs/api-1.0.7.md` tiene firmas verificadas de Container/Inventory/InventoryGui, pero
  **no** de `Smelter`, `Piece`, `ZRoutedRpc`, `ZDO` ni del camino de construccion.
- El mod usa **Krafs.Publicizer** (`QuickStash.csproj`), asi que accede a miembros privados
  del juego (`Smelter.m_nview`, `GetFuel()`, `IsItemAllowed()`) sin reflexion. Al leer el
  decompilado, "private" no significa "el mod no puede tocarlo".

Ver [[verified-vanilla-invariants]] para los hechos del vanilla ya confirmados.
