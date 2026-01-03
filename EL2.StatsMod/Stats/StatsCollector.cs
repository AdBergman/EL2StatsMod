using System;
using System.Collections.Generic;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.UI;
using UnityEngine;

namespace EL2.StatsMod
{
    internal static class StatsCollector
    {
        // [empireIndex] -> [turn] -> TurnStats
        private static readonly Dictionary<int, Dictionary<int, TurnStats>> _statsByEmpireAndTurn
            = new Dictionary<int, Dictionary<int, TurnStats>>();

        internal static IReadOnlyDictionary<int, Dictionary<int, TurnStats>> StatsByEmpireAndTurn => _statsByEmpireAndTurn;

        internal static void Clear() => _statsByEmpireAndTurn.Clear();

        internal static void MergeStatCurves(EndGameStatisticType statType, Vector2[][] graphValuesPerEmpire)
        {
            for (int empireIndex = 0; empireIndex < graphValuesPerEmpire.Length; empireIndex++)
            {
                var curve = graphValuesPerEmpire[empireIndex];
                if (curve == null)
                    continue;

                if (!_statsByEmpireAndTurn.TryGetValue(empireIndex, out var perTurn))
                {
                    perTurn = new Dictionary<int, TurnStats>();
                    _statsByEmpireAndTurn[empireIndex] = perTurn;
                }

                for (int i = 0; i < curve.Length; i++)
                {
                    var point = curve[i];
                    int turn = (int)point.x;
                    float value = point.y;

                    if (!perTurn.TryGetValue(turn, out var ts))
                        ts = new TurnStats();

                    switch (statType)
                    {
                        case EndGameStatisticType.Food:
                            ts.Food = value;
                            break;
                        case EndGameStatisticType.Industry:
                            ts.Industry = value;
                            break;
                        case EndGameStatisticType.Money:
                            ts.Dust = value;
                            break;
                        case EndGameStatisticType.Science:
                            ts.Science = value;
                            break;
                        case EndGameStatisticType.Influence:
                            ts.Influence = value;
                            break;
                        case EndGameStatisticType.Approval:
                            ts.Approval = value;
                            break;
                        case EndGameStatisticType.Populations:
                            ts.Populations = value;
                            break;
                        case EndGameStatisticType.Technologies:
                            ts.Technologies = value;
                            break;
                        case EndGameStatisticType.Units:
                            ts.Units = value;
                            break;
                        case EndGameStatisticType.Cities:
                            ts.Cities = value;
                            break;
                        case EndGameStatisticType.Territories:
                            ts.Territories = value;
                            break;
                        case EndGameStatisticType.Score:
                            ts.Score = value;
                            break;
                        default:
                            // Ignore Count / unknown
                            break;
                    }

                    perTurn[turn] = ts;
                }
            }
        }

        internal static int GetMaxRecordedTurn()
        {
            int maxTurn = 0;
            foreach (var empireEntry in _statsByEmpireAndTurn)
            {
                var perTurn = empireEntry.Value;
                foreach (var turnEntry in perTurn)
                {
                    int t = turnEntry.Key;
                    if (t > maxTurn)
                        maxTurn = t;
                }
            }
            return maxTurn;
        }

        internal static (int topScoreEmpire, float topScore) GetTopScoreEmpireAtTurn(int turn)
        {
            int topScoreEmpire = -1;
            float topScore = float.MinValue;

            foreach (var empireEntry in _statsByEmpireAndTurn)
            {
                int empireIndex = empireEntry.Key;
                var perTurn = empireEntry.Value;

                if (perTurn.TryGetValue(turn, out var ts) && ts.Score.HasValue)
                {
                    float score = ts.Score.Value;
                    if (score > topScore)
                    {
                        topScore = score;
                        topScoreEmpire = empireIndex;
                    }
                }
            }

            return (topScoreEmpire, topScore);
        }

        internal static void ForceCollectAllStats(
            EndGameWindow_GraphsShowable instance,
            EmpireStatistics[] allEmpiresStatistics,
            int numberOfOrdinateTicks)
        {
            if (allEmpiresStatistics == null)
                return;

            var statsToGenerate = new[]
            {
                EndGameStatisticType.Food,
                EndGameStatisticType.Industry,
                EndGameStatisticType.Money,
                EndGameStatisticType.Science,
                EndGameStatisticType.Influence,
                EndGameStatisticType.Approval,
                EndGameStatisticType.Populations,
                EndGameStatisticType.Technologies,
                EndGameStatisticType.Units,
                EndGameStatisticType.Cities,
                EndGameStatisticType.Territories,
                EndGameStatisticType.Score
            };

            foreach (var stat in statsToGenerate)
            {
                try
                {
                    instance.TryReloadGraphValues(allEmpiresStatistics, stat, numberOfOrdinateTicks);
                }
                catch (Exception ex)
                {
                    StatsLoggerPlugin.Log?.LogWarning($"[EL2 Stats] ForceCollectAllStats failed for {stat}: {ex}");
                }
            }
        }
    }
}
