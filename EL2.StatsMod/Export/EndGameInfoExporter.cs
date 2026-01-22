using System;
using System.IO;
using Amplitude.Mercury.Interop;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace EL2.StatsMod.Export
{
    internal static class EndGameInfoExporter
    {
        private const string ExportVersion = "1.0";

        // Single policy point for JSON serialization across the whole export.
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = true,
                    OverrideSpecifiedNames = false
                }
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Creates a single end-game JSON file containing:
        ///   - allStats
        ///   - techOrder
        ///   - cityBreakdown
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

                DateTime generatedAtUtc = DateTime.UtcNow;
                string timestamp = generatedAtUtc.ToString("yyyyMMdd_HHmmss");
                string gameId = "EL2_" + timestamp;
                string generatedAtUtcIso = generatedAtUtc.ToString("o");

                // 1) Build DTO payloads (no JSON strings, no file IO)
                var allStats = CombinedStatsExporter.Export(allEmpiresStatistics);
                var techOrder = TechOrderExporter.Export(allEmpiresStatistics);
                var cityBreakdown = CityBreakdownExporter.Export();

                // 2) Build the root JSON object.
                // Root keys are set explicitly and are already camelCase.
                JObject root = new JObject
                {
                    ["version"] = ExportVersion,
                    ["generatedAtUtc"] = generatedAtUtcIso,
                    ["gameId"] = gameId
                };

                // 3) Attach sections, converting DTOs into JTokens using the shared serializer policy.
                // This is where camelCase gets applied for the entire nested tree.
                JsonSerializer serializer = JsonSerializer.Create(JsonSettings);

                if (allStats != null)
                    root["allStats"] = JToken.FromObject(allStats, serializer);

                if (techOrder != null)
                    root["techOrder"] = JToken.FromObject(techOrder, serializer);

                if (cityBreakdown != null)
                    root["cityBreakdown"] = JToken.FromObject(cityBreakdown, serializer);

                // 4) Serialize once, using the shared settings.
                string finalJson = JsonConvert.SerializeObject(root, JsonSettings);

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
    }
}
