using System;
using System.Collections.Generic;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Dto;
using EL2.StatsMod.Stats;
using Newtonsoft.Json;

namespace EL2.StatsMod.Export
{
    internal static class CombinedStatsExporter
    {
        private const string ExportVersion = "1.0";

        /// <summary>
        /// Builds a JSON string with:
        ///  - global game + victory metadata
        ///  - per-empire metadata (faction, era timing, tech count)
        ///  - per-empire per-turn FIDSI / score from StatsCollector
        /// 
        /// This DOES NOT write files or call other exporters.
        /// It is meant to be used by EndGameInfoExporter.
        /// </summary>
        internal static string ExportToJson(EmpireStatistics[] allEmpiresStatistics)
        {
            try
            {
                AllStatsRoot root = new AllStatsRoot();
                int empireCount = (allEmpiresStatistics != null) ? allEmpiresStatistics.Length : 0;
                root.EmpireCount = empireCount;

                int maxTurn = StatsCollector.GetMaxRecordedTurn();
                root.MaxTurn = maxTurn;

                var topScoreInfo = StatsCollector.GetTopScoreEmpireAtTurn(maxTurn);
                root.TopScoreEmpire = topScoreInfo.topScoreEmpire;
                root.TopScore = topScoreInfo.topScore;

                // --- Game / victory metadata ---
                root.Game = MetadataReaders.ReadGameSettingsMetadata();
                root.Victory = MetadataReaders.ReadVictorySettingsMetadata();

                // --- Per-empire metadata + mapping ---
                List<EmpireStatsEntry> empires = new List<EmpireStatsEntry>();
                Dictionary<int, EmpireStatsEntry> empireMap = new Dictionary<int, EmpireStatsEntry>();

                if (allEmpiresStatistics != null)
                {
                    for (int empireIndex = 0; empireIndex < allEmpiresStatistics.Length; empireIndex++)
                    {
                        EmpireStatsEntry entry = EmpireStatsEntryFactory.Build(
                            empireIndex,
                            ref allEmpiresStatistics[empireIndex],
                            maxTurn
                        );

                        empires.Add(entry);
                        empireMap[empireIndex] = entry;
                    }
                }

                // --- Per-empire, per-turn time series from StatsCollector ---
                foreach (var empireEntry in StatsCollector.StatsByEmpireAndTurn)
                {
                    int empireIndexKey = empireEntry.Key;

                    EmpireStatsEntry targetEmpire;
                    if (!empireMap.TryGetValue(empireIndexKey, out targetEmpire))
                        continue; // Could be a minor empire or something we don't have stats for.

                    if (targetEmpire.PerTurn == null)
                        targetEmpire.PerTurn = new List<TurnStatsEntry>();

                    var perTurnDict = empireEntry.Value;

                    foreach (var turnEntry in perTurnDict)
                    {
                        int turn = turnEntry.Key;
                        var ts = turnEntry.Value;

                        TurnStatsEntry t = new TurnStatsEntry();
                        t.Turn        = turn;
                        t.Food        = (ts.Food         != null) ? ts.Food.Value         : 0f;
                        t.Industry    = (ts.Industry     != null) ? ts.Industry.Value     : 0f;
                        t.Dust        = (ts.Dust         != null) ? ts.Dust.Value         : 0f;
                        t.Science     = (ts.Science      != null) ? ts.Science.Value      : 0f;
                        t.Influence   = (ts.Influence    != null) ? ts.Influence.Value    : 0f;
                        t.Approval    = (ts.Approval     != null) ? ts.Approval.Value     : 0f;
                        t.Populations = (ts.Populations  != null) ? ts.Populations.Value  : 0f;
                        t.Technologies= (ts.Technologies != null) ? ts.Technologies.Value : 0f;
                        t.Units       = (ts.Units        != null) ? ts.Units.Value        : 0f;
                        t.Cities      = (ts.Cities       != null) ? ts.Cities.Value       : 0f;
                        t.Territories = (ts.Territories  != null) ? ts.Territories.Value  : 0f;
                        t.Score       = (ts.Score        != null) ? ts.Score.Value        : 0f;

                        targetEmpire.PerTurn.Add(t);
                    }
                }

                // Sort per-turn arrays for each empire by Turn (ascending)
                for (int i = 0; i < empires.Count; i++)
                {
                    EmpireStatsEntry e = empires[i];
                    if (e.PerTurn != null && e.PerTurn.Count > 1)
                    {
                        e.PerTurn.Sort(
                            delegate (TurnStatsEntry a, TurnStatsEntry b)
                            {
                                return a.Turn.CompareTo(b.Turn);
                            }
                        );
                    }
                }

                root.Empires = empires;

                // --- Serialize ---
                var json = JsonConvert.SerializeObject(root, Formatting.Indented);
                return json;
            }
            catch (Exception ex)
            {
                // Don't crash the game if something goes wrong; just log and return null.
                StatsLoggerPlugin.Log?.LogError("[CombinedStatsExporter] Failed to build JSON: " + ex);
                return null;
            }
        }
    }
}
