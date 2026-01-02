using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Utils;
using Newtonsoft.Json;

internal static class CityBreakdownExporter
{
    // ---------------------------------------------------------------------
    // DTOs for JSON output
    // ---------------------------------------------------------------------

    private sealed class CityBreakdown
    {
        public int CityCount { get; set; }

        public List<CitySummaryEntry> Cities { get; set; }

        public CityBreakdown()
        {
            Cities = new List<CitySummaryEntry>();
        }
    }

    private sealed class CitySummaryEntry
    {
        // Identity / ownership
        public byte EmpireIndex { get; set; }

        public string Name { get; set; }

        public bool IsCapital { get; set; }

        // Scale / size
        public int TerritoryCount { get; set; }
        public int ExtensionDistrictsCount { get; set; }

        public int Population { get; set; }
        public int MaxPopulation { get; set; }

        // Growth / food
        public float FoodStock { get; set; }
        public float MaxFoodStock { get; set; }
        public float FoodGainInPercent { get; set; }
        public float TurnBeforeGrowth { get; set; }
        public string GrowingPopulationName { get; set; }

        // Approval
        public float ApprovalNetInPercent { get; set; }
        public string SettlementApprovalDisplayName { get; set; }

        // Economy
        public float ProductionNet { get; set; }

        // Construction
        public string CurrentConstructibleDisplayName { get; set; }

        // Defense
        public int Fortification { get; set; }
        public int NumberOfPresentMilitiaUnits { get; set; }
        public bool IsBesieged { get; set; }
        public bool IsMutinous { get; set; }

        // Position
        public int DistanceWithCapital { get; set; }
    }

    // ---------------------------------------------------------------------
    // Filtering
    // ---------------------------------------------------------------------

    private static bool IsCity(SettlementInfo settlement)
    {
        return settlement.SettlementStatus == SettlementStatuses.City;
    }

    // ---------------------------------------------------------------------
    // Export entry – return JSON string, no file IO
    // ---------------------------------------------------------------------

    internal static string ExportToJson()
    {
        try
        {
            var gameSnapshot = Snapshots.GameSnapshot;
            if (gameSnapshot == null)
                return null;

            var presentation = gameSnapshot.PresentationData;

            var settlementsFrame = presentation.SettlementInfo;
            var settlements = settlementsFrame.Data;
            int count = settlementsFrame.Length;

            if (settlements == null || count == 0)
                return null;

            List<CitySummaryEntry> cities = new List<CitySummaryEntry>();

            for (int i = 0; i < count; i++)
            {
                SettlementInfo s = settlements[i];

                // Skip "empty" entries
                if (s.SimulationEntityGUID == 0UL)
                    continue;

                // Only export real Cities (filter out outposts/camps/evolving/etc.)
                if (!IsCity(s))
                    continue;

                // Prefer the actual unique name (ToString() is often not what you want here)
                string name = s.EntityName.ToString();
                if (string.IsNullOrEmpty(name))
                    name = "Settlement " + s.SimulationEntityGUID;

                CitySummaryEntry entry = new CitySummaryEntry();

                entry.EmpireIndex = s.EmpireIndex;
                entry.Name = name;
                entry.IsCapital = s.IsCapital;
                entry.TerritoryCount = s.TerritoryCount;
                entry.ExtensionDistrictsCount = s.ExtensionDistrictsCount;
                entry.Population = s.Population;
                entry.MaxPopulation = s.MaxPopulation;
                entry.FoodStock = (float)s.FoodStock;
                entry.MaxFoodStock = (float)s.MaxFoodStock;
                entry.FoodGainInPercent = (float)s.FoodGainInPercent;
                entry.TurnBeforeGrowth = (float)s.TurnBeforeGrowth;
                entry.ProductionNet = (float)s.ProductionNet;
                entry.ApprovalNetInPercent = (float)s.ApprovalNetInPercent;
                
                entry.SettlementApprovalDisplayName =
                    TextFormatUtils.GetLocalizedNameOrNull(
                        s.SettlementApprovalDefinitionName
                    );

                entry.GrowingPopulationName =
                    TextFormatUtils.GetLocalizedNameOrNull(s.GrowingPopulationName)
                    ?? s.GrowingPopulationName.ToString();
                
                entry.CurrentConstructibleDisplayName =
                    TextFormatUtils.GetLocalizedNameOrNull(
                        s.CurrentConstructionInfo.ConstructibleDefinitionName
                    );
                
                entry.Fortification = s.Fortification;
                entry.NumberOfPresentMilitiaUnits = s.NumberOfPresentMilitiaUnits;
                entry.IsBesieged = s.IsBesieged;
                entry.IsMutinous = s.IsMutinous;

                entry.DistanceWithCapital = s.DistanceWithCapital;

                cities.Add(entry);
            }

            if (cities.Count == 0)
                return null;

            CityBreakdown breakdown = new CityBreakdown
            {
                CityCount = cities.Count,
                Cities = cities
            };

            string json = JsonConvert.SerializeObject(breakdown, Formatting.Indented);
            return json;
        }
        catch
        {
            // Fail quietly; EndGameInfoExporter can skip this section.
            return null;
        }
    }
    
    // ---------------------------------------------------------------------
    // Helper method – get pretty UI ConstructibleName from Key
    // ---------------------------------------------------------------------

    private static string GetLocalizedConstructibleName(StaticString constructibleKey)
    {
        try
        {
            var dataUtils = new Amplitude.Mercury.UI.Helpers.DataUtils();

            if (dataUtils.TryGetLocalizedTitle(constructibleKey, out string title))
            {
                return title;
            }
        }
        catch
        {
            // swallow – exporter must never crash the game
        }

        // Fallback: raw key
        return constructibleKey.ToString();
    }

}
