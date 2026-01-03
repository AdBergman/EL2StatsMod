using System;
using System.Collections.Generic;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Dto;
using EL2.StatsMod.Export;
using EL2.StatsMod.Stats;
using EL2.StatsMod.Utils;
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
                root.Version = ExportVersion;
                root.GeneratedAtUtc = DateTime.UtcNow.ToString("o");
                root.GameId = "EL2_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

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
                        EmpireStatsEntry entry = BuildEmpireEntry(
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
                string json = JsonConvert.SerializeObject(root, Formatting.Indented);
                return json;
            }
            catch (Exception ex)
            {
                // Don't crash the game if something goes wrong; just log and return null.
                StatsLoggerPlugin.Log?.LogError("[CombinedStatsExporter] Failed to build JSON: " + ex);
                return null;
            }
        }
        

        // --------------------------------------------------------------------
        // Helpers to construct DTOs
        // --------------------------------------------------------------------


        private static EmpireStatsEntry BuildEmpireEntry(
            int empireIndex,
            ref EmpireStatistics stats,
            int maxTurn)
        {
            EmpireStatsEntry entry = new EmpireStatsEntry();
            entry.EmpireIndex = empireIndex;

            string factionKey = EmpireInfoUtils.ResolveEmpireFactionName(empireIndex);
            entry.FactionKey = factionKey;
            entry.FactionDisplayName = TextFormatUtils.PrettifyKey(factionKey, "Faction_");


            // Tech count
            entry.TechCount = stats.EmpireTechnologyUnlocked.Length;

            // Era timing logic (ported from your CSV exporter, but stored as int[])
            int finalEraIndex = 0;
            int[] cleanedEraTurns = null;

            if (stats.FirstTurnPerEra != null && stats.FirstTurnPerEra.Length > 0)
            {
                int[] eraTurns = stats.FirstTurnPerEra;
                int length = eraTurns.Length;
                cleanedEraTurns = new int[length];

                int lastValidTurn = 0;

                for (int era = 0; era < length; era++)
                {
                    int raw = eraTurns[era];
                    int cleaned = raw;

                    if (era == 0)
                    {
                        // Era 0: always "reached", but sanitize silly values
                        if (cleaned <= 0 || cleaned > maxTurn)
                            cleaned = 1; // fallback

                        lastValidTurn = cleaned;
                        finalEraIndex = 0;
                    }
                    else
                    {
                        // Only accept if:
                        //  - > 0
                        //  - <= maxTurn
                        //  - strictly after previous era's first turn
                        if (cleaned <= 0 || cleaned > maxTurn || cleaned < lastValidTurn)
                        {
                            cleaned = 0; // unreached / bogus
                        }
                        else
                        {
                            finalEraIndex = era;
                            lastValidTurn = cleaned;
                        }
                    }

                    cleanedEraTurns[era] = cleaned;
                }
            }

            entry.FinalEraIndex = finalEraIndex;
            entry.FirstTurnPerEra = cleanedEraTurns;

            // Allocate list for per-turn stats (will be filled later)
            entry.PerTurn = new List<TurnStatsEntry>();

            return entry;
        }
        
    }
}
