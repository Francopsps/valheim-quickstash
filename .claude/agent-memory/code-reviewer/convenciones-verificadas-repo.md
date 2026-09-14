---
name: convenciones-verificadas-repo
description: Convenciones del repo confirmadas leyendo QuickStash y CrewRow — utiles para no marcar como issue lo que es patron establecido.
metadata:
  type: project
---

Confirmado leyendo el codigo de ambos mods (2026-09-14):

- **Cero `var`** en todo el repo (grep: 0 ocurrencias en QuickStash).
- **Cero acentos** en comentarios y strings de codigo; los `.md` de `docs/` **si** llevan acentos.
- `Plugin.cs` es casi identico entre mods: `PatchGroup(string feature, params Type[])` con
  try/catch por grupo, `WarnAboutConflicts()` por substring de GUID, `IsTyping()` con
  Chat/Console/TextInput/StoreGui, `OnDestroy` con `UnpatchSelf`.
- **Interpolacion `$""` solo en `Plugin.cs`**; en features y logging se usa concatenacion `+`
  con guard de config antes de construir el string.
- El `try/catch` del `Update` **no tiene throttle** en QuickStash tampoco (`SafeTick`). Loguear
  60 veces por segundo en el catch de `Update` es el patron establecido: **no reportarlo**.
- `Game_Logout_Patch` con `[HarmonyPatch(typeof(Game), nameof(Game.Logout))]` es el punto de
  limpieza de estado estatico en ambos mods. `Game.Logout` no tiene sobrecargas en 1.0.

**Why:** sin esto se reportan como issue cosas que el usuario ya decidio a proposito.
**How to apply:** antes de marcar un patron en un mod nuevo, grepear el mod hermano maduro;
si el patron existe alli, es convencion, no hallazgo. Relacionado: [[proyecto-mods-valheim]].
