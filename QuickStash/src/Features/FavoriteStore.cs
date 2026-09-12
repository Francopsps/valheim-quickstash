using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace QuickStash.Features
{
    [Serializable]
    internal class FavoriteData
    {
        public List<string> items = new List<string>();
        public List<string> slots = new List<string>();
    }

    /// <summary>
    /// Favoritos del personaje: los items marcados nunca se guardan con el boton,
    /// y las casillas marcadas nunca se vacian.
    ///
    /// Se persiste por personaje en BepInEx\config\QuickStash\favorites.&lt;personaje&gt;.json.
    /// El guardado va con retardo para no escribir a disco en cada clic.
    /// </summary>
    internal static class FavoriteStore
    {
        private const float SaveDelaySeconds = 2f;
        private const float RetryDelaySeconds = 30f;
        private const int MaxLoggedFailures = 3;

        private static readonly HashSet<string> FavoriteItems = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> FavoriteSlots = new HashSet<int>();

        private static string _profileKey;
        private static bool _dirty;
        private static float _saveAt;
        private static int _saveFailures;

        private static string Folder => Path.Combine(Paths.ConfigPath, "QuickStash");

        private static string FilePath =>
            Path.Combine(Folder, "favorites." + (_profileKey ?? "default") + ".json");

        private static int PackSlot(int x, int y) => (y << 8) | (x & 0xFF);

        public static void LoadForCurrentProfile()
        {
            string key = ResolveProfileKey();
            if (key == _profileKey)
            {
                return;
            }

            FlushIfDirty();

            _profileKey = key;
            FavoriteItems.Clear();
            FavoriteSlots.Clear();

            try
            {
                if (File.Exists(FilePath))
                {
                    FavoriteData data = JsonUtility.FromJson<FavoriteData>(File.ReadAllText(FilePath));
                    if (data != null)
                    {
                        if (data.items != null)
                        {
                            foreach (string item in data.items)
                            {
                                FavoriteItems.Add(item);
                            }
                        }

                        if (data.slots != null)
                        {
                            foreach (string slot in data.slots)
                            {
                                string[] parts = slot.Split(',');
                                if (parts.Length == 2 &&
                                    int.TryParse(parts[0], out int x) &&
                                    int.TryParse(parts[1], out int y))
                                {
                                    FavoriteSlots.Add(PackSlot(x, y));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"No se pudieron leer los favoritos de '{_profileKey}': {e.Message}");
            }

            _dirty = false;
            _saveFailures = 0;
        }

        private static string ResolveProfileKey()
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null)
            {
                return null;
            }

            string name = profile.GetFilename();
            if (string.IsNullOrEmpty(name))
            {
                name = profile.GetName();
            }

            if (string.IsNullOrEmpty(name))
            {
                return "default";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        public static bool IsFavoriteItem(ItemDrop.ItemData item)
        {
            return item?.m_shared != null && FavoriteItems.Contains(item.m_shared.m_name);
        }

        public static bool IsFavoriteSlot(int x, int y)
        {
            return FavoriteSlots.Contains(PackSlot(x, y));
        }

        /// <summary>Un item esta protegido por su tipo o por la casilla en la que esta.</summary>
        public static bool IsProtected(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }

            return IsFavoriteItem(item) || IsFavoriteSlot(item.m_gridPos.x, item.m_gridPos.y);
        }

        public static bool ToggleItem(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            string name = item.m_shared.m_name;
            bool added;
            if (FavoriteItems.Contains(name))
            {
                FavoriteItems.Remove(name);
                added = false;
            }
            else
            {
                FavoriteItems.Add(name);
                added = true;
            }

            MarkDirty();
            return added;
        }

        public static bool ToggleSlot(int x, int y)
        {
            int packed = PackSlot(x, y);
            bool added;
            if (FavoriteSlots.Contains(packed))
            {
                FavoriteSlots.Remove(packed);
                added = false;
            }
            else
            {
                FavoriteSlots.Add(packed);
                added = true;
            }

            MarkDirty();
            return added;
        }

        private static void MarkDirty()
        {
            _dirty = true;
            _saveAt = Time.realtimeSinceStartup + SaveDelaySeconds;
        }

        /// <summary>Llamado desde el Update del plugin. Escribe a disco como mucho cada 2 segundos.</summary>
        public static void Tick()
        {
            if (_dirty && Time.realtimeSinceStartup >= _saveAt)
            {
                Save();
            }
        }

        public static void FlushIfDirty()
        {
            if (_dirty)
            {
                Save();
            }
        }

        private static void Save()
        {
            if (_profileKey == null)
            {
                _dirty = false;
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);

                FavoriteData data = new FavoriteData();
                data.items.AddRange(FavoriteItems);
                foreach (int packed in FavoriteSlots)
                {
                    data.slots.Add((packed & 0xFF) + "," + (packed >> 8));
                }

                // Escritura atomica: si el juego se corta a mitad, el que queda incompleto es
                // el .tmp y el archivo bueno sigue intacto. Escribir directo sobre el destino
                // deja un JSON truncado y el jugador pierde todos sus favoritos.
                string tempPath = FilePath + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                File.Move(tempPath, FilePath);

                _dirty = false;
                _saveFailures = 0;
            }
            catch (Exception e)
            {
                // _dirty queda en true a proposito: el cambio se reintenta mas tarde en vez de
                // perderse en silencio. Se espacia el reintento para no llenar el log.
                _saveFailures++;
                _saveAt = Time.realtimeSinceStartup + RetryDelaySeconds;

                if (_saveFailures <= MaxLoggedFailures)
                {
                    Plugin.Log.LogWarning($"No se pudieron guardar los favoritos de '{_profileKey}': {e.Message}");
                }
            }
        }
    }
}
