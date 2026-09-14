---
name: valheim-decompile-workflow
description: Como decompilar y compilar en este repo — comandos exactos que funcionan en esta maquina.
metadata:
  type: reference
---

- Decompilar: `cd "J:/Mod Valheim/refs/Managed" && PATH="/c/Program Files/dotnet:$PATH:$HOME/.dotnet/tools" ilspycmd assembly_valheim.dll -t <Tipo>`. Volcá a un archivo en el scratchpad y grepealo; los tipos grandes (Inventory, ItemDrop, Player) desbordan el contexto si los leés enteros.
- `ItemDrop` y `Inventory` estan en el **mismo** archivo decompilado que otros tipos; usá `grep -n` con la firma exacta, no `awk` de rango a ciegas.
- Compilar: `powershell -File QuickStash/build.ps1 -NoZip` (rapido, ~1 s, y es legitimo hacerlo).
- El `.csproj` usa **Krafs.Publicizer** sobre `assembly_valheim`, `assembly_utils`, `assembly_guiutils`. Por eso el mod llama miembros `private` del juego (`MonsterAI.CanConsume`, `BaseAI.m_instances`, `Container.m_nview`) sin reflexion. **No lo reportes como error de compilacion ni supongas reflexion en runtime.**
- `docs/api-1.0.7.md` tiene firmas ya verificadas, incluida una seccion de que se puede leer sin ser dueño del ZDO. Consultalo antes de decompilar.

Relacionado: [[valheim-vanilla-gotchas]]
