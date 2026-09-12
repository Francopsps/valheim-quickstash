using System;
using System.Collections.Generic;
using System.Text;
using QuickStash.Config;
using UnityEngine;

namespace QuickStash.Core
{
    /// <summary>
    /// Deja en el log de BepInEx todo lo que el mod mueve entre el inventario y los cofres.
    ///
    /// Existe para poder auditar: si a alguien le falta un item, se busca en
    /// BepInEx\LogOutput.log que se movio, cuanto, a que cofre y a que hora. Va una linea por
    /// cofre, no por item, para que el log siga siendo legible.
    /// </summary>
    internal static class MoveLog
    {
        private static readonly StringBuilder Builder = new StringBuilder(128);

        public static void Record(bool intoContainer, Container container, Dictionary<string, int> items)
        {
            if (!PluginConfig.LogMoves.Value || items == null || items.Count == 0)
            {
                return;
            }

            try
            {
                Builder.Length = 0;
                foreach (KeyValuePair<string, int> entry in items)
                {
                    if (Builder.Length > 0)
                    {
                        Builder.Append(", ");
                    }

                    Builder.Append(entry.Key).Append(" x").Append(entry.Value);
                }

                string direction = intoContainer ? "GUARDADO ->" : "SACADO <-";
                Plugin.Log.LogInfo($"{direction} {Describe(container)} | {DateTime.Now:HH:mm:ss} | {Builder}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"No se pudo registrar el movimiento: {e.Message}");
            }
            finally
            {
                Builder.Length = 0;
            }
        }

        public static void RecordSingle(bool intoContainer, Container container, string itemName, int amount)
        {
            if (!PluginConfig.LogMoves.Value || amount <= 0)
            {
                return;
            }

            string direction = intoContainer ? "GUARDADO ->" : "SACADO <-";
            Plugin.Log.LogInfo(
                $"{direction} {Describe(container)} | {DateTime.Now:HH:mm:ss} | {Localize(itemName)} x{amount}");
        }

        /// <summary>Nombre del cofre mas su posicion redondeada, para poder ir a buscarlo al mundo.</summary>
        private static string Describe(Container container)
        {
            if (container == null)
            {
                return "cofre desconocido";
            }

            Vector3 position = container.transform.position;
            return $"{Localize(container.m_name)} ({position.x:F0}, {position.y:F0}, {position.z:F0})";
        }

        public static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return "?";
            }

            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
