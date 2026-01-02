// using System;
// using System.Collections.Generic;
// using System.IO;
// using Amplitude.Mercury.Game;
// using Amplitude.Mercury.Interop;
// using UnityEngine;
// using Newtonsoft.Json;
//
// namespace EndlessLegend2.Exporters
// {
//     /// <summary>
//     /// Exports a basic per-empire / per-city science breakdown to JSON.
//     /// - Uses GameSnapshot.PresentationData (no internal tooltip services).
//     /// - "Basic" = one record per empire, with a list of its cities / settlements.
//     /// - You call Export(outputDir, turnIndex) from your ExportData pipeline.
//     /// </summary>
//     public static class ScienceBreakdownExporter
//     {
//         /// <summary>
//         /// Entry point you call from your ExportData plugin.
//         /// </summary>
//         /// <param name="outputDirectory">Directory where the JSON file is written.</param>
//         /// <param name="turnIndex">
//         /// Turn index to tag the file with (pass whatever you already use in your other exporters).
//         /// </param>
//         public static void Export(string outputDirectory, int turnIndex)
//         {
//             try
//             {
//                 // Make sure directory exists
//                 if (!Directory.Exists(outputDirectory))
//                 {
//                     Directory.CreateDirectory(outputDirectory);
//                 }
//
//                 var breakdown = BuildScienceBreakdown(turnIndex);
//
//                 string json = JsonConvert.SerializeObject(breakdown, Formatting.Indented);
//
//                 string fileName = $"science_breakdown_turn_{turnIndex}.json";
//                 string fullPath = Path.Combine(outputDirectory, fileName);
//
//                 File.WriteAllText(fullPath, json);
//
//                 Debug.Log($"[ScienceBreakdownExporter] Wrote science breakdown to: {fullPath}");
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"[ScienceBreakdownExporter] Failed to export science breakdown: {ex}");
//             }
//         }
//
//         private static ScienceBreakdownRoot BuildScienceBreakdown(int turnIndex)
//         {
//             var root = new ScienceBreakdownRoot
//             {
//                 Turn = turnIndex,
//                 GeneratedAt = DateTime.UtcNow.ToString("o"),
//                 Empires = new List<EmpireScienceBreakdown>()
//             };
//
//             // Grab snapshot once
//             var gameSnapshot = Snapshots.GameSnapshot;
//             var presentation = gameSnapshot.PresentationData;
//
//             // NOTE: These two arrays exist in almost all Amplitude games.
//             // If any name differs in EL2, adjust them to match your decompiled snapshot type.
//             EmpireInfo[] empires = presentation.EmpireInfo;
//             // Assumption: there is a SettlementInfo[] in PresentationData (very standard).
//             // If your decompile shows a different name, change it here.
//             SettlementInfo[] settlements = presentation.SettlementInfo;
//
//             // Build quick lookup: empire index -> list of its settlements
//             var settlementsByEmpire = new Dictionary<byte, List<SettlementInfo>>();
//             foreach (var s in settlements)
//             {
//                 // Assumption: SettlementInfo has a byte EmpireIndex like EmpireInfo.
//                 byte empireIndex = s.EmpireIndex;
//                 if (!settlementsByEmpire.TryGetValue(empireIndex, out var list))
//                 {
//                     list = new List<SettlementInfo>();
//                     settlementsByEmpire[empireIndex] = list;
//                 }
//                 list.Add(s);
//             }
//
//             for (int i = 0; i < empires.Length; i++)
//             {
//                 ref EmpireInfo empire = ref empires[i];
//
//                 // Skip dead / lesser empires if you don't care about them
//                 if (!empire.IsAlive || empire.IsLesser)
//                     continue;
//
//                 var empireEntry = new EmpireScienceBreakdown
//                 {
//                     EmpireIndex = empire.EmpireIndex,
//                     FactionDefinition = empire.CurrentFactionDefinitionName.ToString(),
//                     FactionAffinity = empire.CurrentFactionAffinityName.ToString(),
//                     UserName = empire.UserName,
//                     PersonaName = empire.PersonaName,
//                     // Empire-level science data
//                     ResearchNet = (float)empire.ResearchNet,
//                     ResearchStock = (float)empire.ResearchStock,
//                     DeviatedResearchNet = (float)empire.DeviatedResearchNet,
//                     Fame = (float)empire.FameStock,
//                     Score = (float)empire.Score,
//                     Cities = new List<CityScienceEntry>()
//                 };
//
//                 if (settlementsByEmpire.TryGetValue(empire.EmpireIndex, out var empireSettlements))
//                 {
//                     foreach (var s in empireSettlements)
//                     {
//                         // --- ASSUMPTIONS about SettlementInfo fields ---
//                         // Adjust these names if your decompile differs.
//                         //
//                         // Very typical fields:
//                         //  - byte EmpireIndex;
//                         //  - StaticString Name;
//                         //  - FixedPoint ResearchNet (or ScienceNet);
//                         //  - bool IsCity / IsSettlement / IsTradingPost, etc.
//                         //
//                         // For "basic" we don't strictly need to filter to only cities,
//                         // but if SettlementInfo has an IsCity flag, you can uncomment.
//
//                         // If SettlementInfo has IsCity: filter
//                         // if (!s.IsCity) continue;
//
//                         var cityEntry = new CityScienceEntry
//                         {
//                             Name = s.Name.ToString(),         // assumes StaticString Name
//                             SettlementIndex = s.SettlementIndex, // assumes int SettlementIndex
//                             ResearchNet = (float)s.ResearchNet   // assumes FixedPoint ResearchNet
//                         };
//
//                         empireEntry.Cities.Add(cityEntry);
//                     }
//                 }
//
//                 // Just to have a quick check that city sum ≈ empire ResearchNet (sanity)
//                 float sumCities = 0f;
//                 foreach (var c in empireEntry.Cities)
//                 {
//                     sumCities += c.ResearchNet;
//                 }
//                 empireEntry.SumCityResearchNet = sumCities;
//
//                 root.Empires.Add(empireEntry);
//             }
//
//             return root;
//         }
//
//         #region DTOs
//
//         [Serializable]
//         public class ScienceBreakdownRoot
//         {
//             public int Turn;
//             public string GeneratedAt;
//             public List<EmpireScienceBreakdown> Empires;
//         }
//
//         [Serializable]
//         public class EmpireScienceBreakdown
//         {
//             public byte EmpireIndex;
//
//             public string FactionDefinition;
//             public string FactionAffinity;
//
//             public string UserName;
//             public string PersonaName;
//
//             // Empire-level science
//             public float ResearchNet;
//             public float ResearchStock;
//             public float DeviatedResearchNet;
//
//             // Extra context, can be useful to correlate with victory / performance
//             public float Fame;
//             public float Score;
//
//             // Sanity check: sum of cities' ResearchNet
//             public float SumCityResearchNet;
//
//             public List<CityScienceEntry> Cities;
//         }
//
//         [Serializable]
//         public class CityScienceEntry
//         {
//             public string Name;
//             public int SettlementIndex;
//             public float ResearchNet;
//         }
//
//         #endregion
//     }
// }
