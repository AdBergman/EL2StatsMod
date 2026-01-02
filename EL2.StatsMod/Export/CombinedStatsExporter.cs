using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Framework.Session;
using Amplitude.Mercury.Analytics;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Utils;
using Newtonsoft.Json;
using MercuryMetadataKeys = Amplitude.Mercury.Session.MetadataKeys;

namespace EL2.StatsMod
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

                var winnerInfo = StatsCollector.GetWinnerAtTurn(maxTurn);
                root.WinnerEmpire = winnerInfo.winnerEmpire;
                root.WinnerScore = winnerInfo.winnerScore;

                // --- Game / victory metadata ---
                GameSettings game = ReadGameSettingsMetadata();
                VictorySettings victory = ReadVictorySettingsMetadata();
                root.Game = game;
                root.Victory = victory;

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
        // DTOs (simple classes / fields so they work with older C# / Json.NET)
        // --------------------------------------------------------------------

        private class AllStatsRoot
        {
            public string Version;
            public string GeneratedAtUtc;
            public string GameId;

            public int EmpireCount;
            public int MaxTurn;
            public int WinnerEmpire;
            public float WinnerScore;

            public GameSettings Game;
            public VictorySettings Victory;

            public List<EmpireStatsEntry> Empires;
        }

        private class GameSettings
        {
            public string Difficulty;
            public string MapSize;
            public string GameSpeed;
        }

        private class VictorySettings
        {
            public string VictoryPreset;
            public string ActualVictoryCondition;
            public string VictoryConditionsEnabled;
        }

        private class EmpireStatsEntry
        {
            public int EmpireIndex;

            public string FactionKey;          // e.g. "Faction_Necrophage"
            public string FactionDisplayName;  // e.g. "Necrophage"

            public int TechCount;
            public int FinalEraIndex;
            public int[] FirstTurnPerEra;      // clean, per-era first turn (0 if never reached / invalid)

            public List<TurnStatsEntry> PerTurn;
        }

        private class TurnStatsEntry
        {
            public int Turn;

            public float Food;
            public float Industry;
            public float Dust;
            public float Science;
            public float Influence;
            public float Approval;
            public float Populations;
            public float Technologies;
            public float Units;
            public float Cities;
            public float Territories;
            public float Score;
        }

        // --------------------------------------------------------------------
        // Helpers to construct DTOs
        // --------------------------------------------------------------------

        private static GameSettings ReadGameSettingsMetadata()
        {
            GameSettings game = new GameSettings();
            game.Difficulty = "Unknown";
            game.MapSize    = "Unknown";
            game.GameSpeed  = "Unknown";

            try
            {
                var sessionService = Services.GetService<ISessionService>();
                var metadata = (sessionService != null) ? sessionService.Session.Metadata : null;

                if (metadata != null)
                {
                    string value;

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.GameDifficulty, out value))
                        game.Difficulty = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.WorldSize, out value))
                        game.MapSize = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.GameSpeed, out value))
                        game.GameSpeed = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning("[CombinedStatsExporter] Failed to read session metadata: " + ex.Message);
            }

            return game;
        }


        private static VictorySettings ReadVictorySettingsMetadata()
        {
            VictorySettings victory = new VictorySettings();
            victory.VictoryPreset = "Unknown";
            victory.ActualVictoryCondition = "Unknown";
            victory.VictoryConditionsEnabled = "Unknown";

            try
            {
                var vcSnapshot = Snapshots.VictoryConditionsWindowSnapshot;
                if (vcSnapshot != null)
                {
                    var data = vcSnapshot.PresentationData;

                    // Preset: EndGameDefinitionName (chosen in game setup)
                    StaticString defName = data.EndGameDefinitionName;
                    if (!StaticString.IsNullOrEmpty(defName))
                        victory.VictoryPreset = defName.ToString();

                    // Actual victory condition that fired
                    victory.ActualVictoryCondition = data.EndGameConditionType.ToString();

                    // Enabled conditions, prefer AnalyticsEvent_GameCreated helper
                    try
                    {
                        string detail = AnalyticsEvent_GameCreated.GatherEndGameConditionActivation();
                        if (!string.IsNullOrEmpty(detail))
                        {
                            victory.VictoryConditionsEnabled = detail;
                        }
                    }
                    catch
                    {
                        // Fallback: build from EndGameConditionsInfo
                        var infos = data.EndGameConditionsInfo;
                        if (infos != null && infos.Length > 0)
                        {
                            System.Text.StringBuilder sb = new System.Text.StringBuilder();
                            for (int i = 0; i < infos.Length; i++)
                            {
                                if (i > 0)
                                    sb.Append(';');

                                var info = infos[i];
                                sb.Append(info.ConditionType);
                                sb.Append(':');
                                sb.Append(info.IsEnabled ? "True" : "False");
                            }

                            victory.VictoryConditionsEnabled = sb.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning("[CombinedStatsExporter] Failed to read victory settings: " + ex.Message);
            }

            return victory;
        }

        private static EmpireStatsEntry BuildEmpireEntry(
            int empireIndex,
            ref EmpireStatistics stats,
            int maxTurn)
        {
            EmpireStatsEntry entry = new EmpireStatsEntry();
            entry.EmpireIndex = empireIndex;

            string factionKey = ResolveEmpireFactionName(empireIndex);
            entry.FactionKey = factionKey;
            entry.FactionDisplayName = Utils.TextFormatUtils.PrettifyKey(factionKey, "Faction_");


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

        // --------------------------------------------------------------------
        // Existing helpers copied from your original exporter
        // --------------------------------------------------------------------

        /// <summary>
        /// Best-effort attempt to get the major faction name for a given empire index.
        /// 1) Try GameSnapshot.PresentationData.EmpireInfo[...] via reflection (field with "Faction" in name).
        /// 2) Fallback to Sandbox.MajorEmpires as before.
        /// Returns "Unknown" if anything fails.
        /// </summary>
        private static string ResolveEmpireFactionName(int empireIndex)
        {
            // --- 1) Try GameSnapshot EmpireInfo ---

            try
            {
                var gameSnapshot = Snapshots.GameSnapshot;
                if (gameSnapshot != null)
                {
                    var data = gameSnapshot.PresentationData;
                    var empireInfos = data.EmpireInfo;

                    if (empireInfos != null &&
                        empireIndex >= 0 &&
                        empireIndex < empireInfos.Length)
                    {
                        var ei = empireInfos[empireIndex];
                        var eiType = ei.GetType();

                        // Prefer StaticString fields with "Faction" in the name
                        FieldInfo[] fields = eiType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        int i;

                        for (i = 0; i < fields.Length; i++)
                        {
                            FieldInfo field = fields[i];
                            if (field.FieldType == typeof(StaticString) &&
                                field.Name.IndexOf("Faction", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                StaticString val = (StaticString)field.GetValue(ei);
                                if (!StaticString.IsNullOrEmpty(val))
                                    return val.ToString();
                            }
                        }

                        // Fallback: string fields with "Faction" or "Empire" in the name
                        for (i = 0; i < fields.Length; i++)
                        {
                            FieldInfo field = fields[i];
                            if (field.FieldType == typeof(string) &&
                                (field.Name.IndexOf("Faction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 field.Name.IndexOf("Empire", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                string strVal = field.GetValue(ei) as string;
                                if (!string.IsNullOrEmpty(strVal))
                                    return strVal;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning(
                    "[CombinedStatsExporter] Failed to resolve empire faction from GameSnapshot for index " +
                    empireIndex + ": " + ex.Message
                );
            }

            // --- 2) Fallback: old Sandbox.MajorEmpires reflection ---

            try
            {
                Type sandboxType = Type.GetType(
                    "Amplitude.Mercury.Sandbox.Sandbox, Amplitude.Mercury.Firstpass",
                    false
                );

                if (sandboxType == null)
                    return "Unknown";

                FieldInfo majorEmpiresField = sandboxType.GetField(
                    "MajorEmpires",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (majorEmpiresField == null)
                    return "Unknown";

                Array majorEmpires = majorEmpiresField.GetValue(null) as Array;
                if (majorEmpires == null)
                    return "Unknown";

                if (empireIndex < 0 || empireIndex >= majorEmpires.Length)
                    return "Unknown";

                object majorEmpire = majorEmpires.GetValue(empireIndex);
                if (majorEmpire == null)
                    return "Unknown";

                PropertyInfo majorFactionDefProp = majorEmpire.GetType().GetProperty(
                    "MajorFactionDefinition",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (majorFactionDefProp == null)
                    return "Unknown";

                object majorFactionDef = majorFactionDefProp.GetValue(majorEmpire, null);
                if (majorFactionDef == null)
                    return "Unknown";

                PropertyInfo nameProp = majorFactionDef.GetType().GetProperty(
                    "Name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (nameProp == null)
                    return "Unknown";

                object staticStringValue = nameProp.GetValue(majorFactionDef, null);
                if (staticStringValue == null)
                    return "Unknown";

                return staticStringValue.ToString();
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning(
                    "[CombinedStatsExporter] Failed to resolve empire faction name for index " +
                    empireIndex + ": " + ex.Message
                );
                return "Unknown";
            }
        }
    }
}
