using System;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.UI;
using HarmonyLib;
using UnityEngine;
using BepInEx;           // for Paths
using BepInEx.Logging;   // for ManualLogSource (used by EndGameInfoExporter)

namespace EL2.StatsMod
{
    [HarmonyPatch(typeof(EndGameWindow_GraphsShowable), "TryReloadGraphValues")]
    public static class EndGameGraphsPatch
    {
        private static bool _dumpedThisSession = false;
        private static bool _autoCollectRunning = false;

        // Signature must match original + __result
        static void Postfix(
            EndGameWindow_GraphsShowable __instance,
            EmpireStatistics[] allEmpiresStatistics,
            EndGameStatisticType statType,
            int numberOfOrdinateTicks,
            bool __result)
        {
            if (!__result)
                return;

            try
            {
                // Read private graphValuesPerEmpire field
                var field = AccessTools.Field(typeof(EndGameWindow_GraphsShowable), "graphValuesPerEmpire");
                var graphValuesPerEmpire = field.GetValue(__instance) as Vector2[][];

                if (graphValuesPerEmpire == null)
                {
                    StatsLoggerPlugin.Log?.LogWarning("[EL2 Stats] graphValuesPerEmpire was null.");
                    return;
                }

                // Always merge the current graph into our data structure
                StatsCollector.MergeStatCurves(statType, graphValuesPerEmpire);

                // If we're in the middle of auto-collect, do NOT trigger recursion/dump again
                if (_autoCollectRunning)
                    return;

                // If we've already dumped once this session, don't do it again
                if (_dumpedThisSession)
                    return;

                // When the Score graph is built the first time, auto-generate all stats and dump
                if (statType == EndGameStatisticType.Score)
                {
                    _autoCollectRunning = true;
                    try
                    {
                        StatsLoggerPlugin.Log?.LogInfo(
                            "[EL2 Stats] Auto-collecting all end-game stats via TryReloadGraphValues..."
                        );

                        // Use the real array & ticks
                        StatsCollector.ForceCollectAllStats(__instance, allEmpiresStatistics, numberOfOrdinateTicks);

                        // After ForceCollectAllStats, our postfix has merged curves for all stat types
                        // into StatsCollector.StatsByEmpireAndTurn.

                        // 🚀 New unified JSON exporter
                        EndGameInfoExporter.Export(
                            Paths.BepInExRootPath,
                            StatsLoggerPlugin.Log,
                            allEmpiresStatistics
                        );

                        _dumpedThisSession = true;
                        StatsCollector.Clear();
                    }
                    finally
                    {
                        _autoCollectRunning = false;
                    }
                }
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogError($"[EL2 Stats] Exception in TryReloadGraphValues postfix: {ex}");
            }
        }
    }
}
