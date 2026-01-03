using System;
using System.IO;
using Amplitude.Mercury.Interop;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
// EmpireStatistics

// Debug.LogException

namespace EL2.StatsMod.Export
{
    internal static class EndGameInfoExporter
    {
        private const string ExportVersion = "1.0";

        /// <summary>
        /// Creates a single end-game JSON file containing:
        ///   - allStats   (from CombinedStatsExporter.ExportToJson)
        ///   - techOrder  (from TechOrderExporter.ExportToJson)
        ///   - cityBreakdown (from CityBreakdownExporter.ExportToJson)
        ///
        /// allEmpiresStatistics is the same array you previously passed
        /// into CombinedStatsExporter / TechOrderExporter.
        /// </summary>
        public static void Export(
            string outputDirectory,
            ManualLogSource logger,
            EmpireStatistics[] allEmpiresStatistics)
        {
            try
            {
                if (allEmpiresStatistics == null || allEmpiresStatistics.Length == 0)
                {
                    logger?.LogWarning(
                        "[EndGameInfoExporter] allEmpiresStatistics is null or empty – aborting export."
                    );
                    return;
                }

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

                // 1) Get JSON from the three exporters.
                //    These should return JSON strings, not write their own files.
                string allStatsJson      = CombinedStatsExporter.ExportToJson(allEmpiresStatistics);
                string techOrderJson     = TechOrderExporter.ExportToJson(allEmpiresStatistics);
                string cityBreakdownJson = CityBreakdownExporter.ExportToJson();

                // 2) Parse into JTokens so we can nest them nicely.
                //    If any exporter returns null/empty, we omit that section.
                JToken allStatsToken      = ParseOrNull(allStatsJson, "allStats", logger);
                JToken techOrderToken     = ParseOrNull(techOrderJson, "techOrder", logger);
                JToken cityBreakdownToken = ParseOrNull(cityBreakdownJson, "cityBreakdown", logger);

                // 3) Build the root object.
                JObject root = new JObject
                {
                    ["version"]        = ExportVersion,
                    ["generatedAtUtc"] = DateTime.UtcNow.ToString("o") // ISO-8601
                };

                // Optional: game-level metadata hook (fill when ready)
                JObject gameMeta = new JObject();
                // Example placeholders, once you wire them up:
                // gameMeta["difficulty"] = "Serious";
                // gameMeta["mapSize"]    = "Large";
                // gameMeta["modVersion"] = StatsLoggerPlugin.PluginVersion;
                if (gameMeta.HasValues)
                    root["game"] = gameMeta;

                if (allStatsToken != null)
                    root["allStats"] = allStatsToken;

                if (techOrderToken != null)
                    root["techOrder"] = techOrderToken;

                if (cityBreakdownToken != null)
                    root["cityBreakdown"] = cityBreakdownToken;

                // 4) Serialize the whole thing.
                string finalJson = JsonConvert.SerializeObject(root, Formatting.Indented);

                // 5) Write one file.
                string fileName = "EL2_EndGame_" + timestamp + ".json";
                string filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllText(filePath, finalJson);

                logger?.LogInfo("[EndGameInfoExporter] Saved combined end-game JSON to " + filePath);
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogError("[EndGameInfoExporter] Failed to export combined end-game JSON: " + ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Parses a JSON string into a JToken.
        /// Returns null if the input is null/empty.
        /// Logs a warning if parsing fails.
        /// </summary>
        private static JToken ParseOrNull(string json, string sectionName, ManualLogSource logger)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JToken.Parse(json);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    "[EndGameInfoExporter] Failed to parse JSON returned from " +
                    sectionName + " exporter: " + ex.Message
                );
                return null;
            }
        }
    }
}
