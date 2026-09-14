using System.Collections.Generic;
using System.Text;
using QuickStash.Config;
using UnityEngine;

namespace QuickStash.Core
{
    /// <summary>
    /// Volcado periodico del estado real de los animales domesticados cercanos.
    ///
    /// Existe porque el enganche de alimentacion (postfix de
    /// MonsterAI.FindClosestConsumableItem) puede no ejecutarse nunca, y cuando eso pasa el log
    /// queda en silencio absoluto: no se distingue "el plugin no cargo" de "no somos duenos del
    /// ZDO del animal" de "la lista de comida del bicho esta vacia". Este barrido mira los tres
    /// a la vez, sin depender de que el enganche funcione.
    ///
    /// Usa BaseAI.m_instances, la lista estatica que el propio juego mantiene en Awake/OnDestroy,
    /// asi que no hace falta ningun FindObjectsOfType.
    /// </summary>
    internal static class AnimalDiagnostics
    {
        private const float IntervalSeconds = 5f;
        private const float ScanRange = 25f;
        private const int MaxReported = 5;

        private static readonly StringBuilder Builder = new StringBuilder(160);
        private static readonly List<Container> ContainerBuffer = new List<Container>(8);

        private static float _nextRun;

        /// <summary>Para no repetir "no hay animales" cada 5 s: solo se avisa al cambiar de estado.</summary>
        private static bool _reportedEmpty;

        public static void Tick()
        {
            if (!PluginConfig.DebugTiming.Value)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextRun)
            {
                return;
            }

            _nextRun = now + IntervalSeconds;

            List<BaseAI> instances = BaseAI.m_instances;
            if (instances == null)
            {
                Plugin.Log.LogWarning("[diag] no se pudo leer la lista de IAs del juego.");
                return;
            }

            Vector3 origin = player.transform.position;
            float sqrRange = ScanRange * ScanRange;
            long playerId = ContainerAccess.LocalPlayerId();
            int reported = 0;
            int tamedNearby = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                BaseAI ai = instances[i];
                if (ai == null)
                {
                    continue;
                }

                Tameable tameable = ai.m_tamable;
                if (tameable == null || !tameable.IsTamed())
                {
                    continue;
                }

                if ((ai.transform.position - origin).sqrMagnitude > sqrRange)
                {
                    continue;
                }

                tamedNearby++;
                if (reported >= MaxReported)
                {
                    continue;
                }

                reported++;
                Report(ai, tameable, origin, playerId);
            }

            if (tamedNearby == 0)
            {
                if (!_reportedEmpty)
                {
                    _reportedEmpty = true;
                    Plugin.Log.LogInfo($"[diag] no hay animales domesticados a menos de {ScanRange:F0} m.");
                }

                return;
            }

            _reportedEmpty = false;

            if (tamedNearby > reported)
            {
                Plugin.Log.LogInfo($"[diag] ... y {tamedNearby - reported} animal(es) mas, no listados.");
            }
        }

        private static void Report(BaseAI ai, Tameable tameable, Vector3 origin, long playerId)
        {
            ZNetView nview = ai.m_nview;
            bool owner = nview != null && nview.IsValid() && nview.IsOwner();

            Builder.Length = 0;
            Builder.Append("[diag] ").Append(NameOf(ai));
            Builder.Append(" a ").Append(Vector3.Distance(ai.transform.position, origin).ToString("F1")).Append(" m");

            // Este es EL dato: si no somos duenos del ZDO, BaseAI.UpdateAI corta antes de
            // buscar comida y el enganche del mod no llega a ejecutarse nunca.
            Builder.Append(" | dueno del ZDO: ").Append(owner ? "SI" : "NO");
            Builder.Append(" | hambriento: ").Append(tameable.IsHungry() ? "si" : "no");

            MonsterAI monster = ai as MonsterAI;
            if (monster == null)
            {
                Builder.Append(" | no es MonsterAI (no usa el sistema de comer del suelo)");
            }
            else if (monster.m_consumeItems == null || monster.m_consumeItems.Count == 0)
            {
                Builder.Append(" | LISTA DE COMIDA VACIA (el juego nunca busca comida para este bicho)");
            }
            else
            {
                Builder.Append(" | come ").Append(monster.m_consumeItems.Count).Append(": ");
                for (int i = 0; i < monster.m_consumeItems.Count; i++)
                {
                    ItemDrop food = monster.m_consumeItems[i];

                    // Un mod de criaturas puede dejar entradas incompletas en la lista.
                    if (food == null || food.m_itemData?.m_shared == null)
                    {
                        continue;
                    }

                    if (i > 0)
                    {
                        Builder.Append(", ");
                    }

                    Builder.Append(MoveLog.Localize(food.m_itemData.m_shared.m_name));
                }
            }

            ContainerRegistry.Query(
                ai.transform.position,
                PluginConfig.AnimalFeedRange.Value,
                PluginConfig.MaxScanned.Value,
                playerId,
                ContainerBuffer);

            int owned = 0;
            foreach (Container container in ContainerBuffer)
            {
                if (container.m_nview != null && container.m_nview.IsOwner())
                {
                    owned++;
                }
            }

            Builder.Append(" | cofres accesibles: ").Append(ContainerBuffer.Count)
                   .Append(" (tuyos: ").Append(owned).Append(')');

            Plugin.Log.LogInfo(Builder.ToString());
            Builder.Length = 0;
        }

        private static string NameOf(BaseAI ai)
        {
            Character character = ai.m_character;
            return character != null ? MoveLog.Localize(character.m_name) : ai.name;
        }

        public static void Reset()
        {
            _nextRun = 0f;
            _reportedEmpty = false;
            ContainerBuffer.Clear();
        }
    }
}
