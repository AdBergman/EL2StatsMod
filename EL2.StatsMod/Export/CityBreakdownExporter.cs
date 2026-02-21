using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Utils;

namespace EL2.StatsMod.Export
{
    internal static class CityBreakdownExporter
    {
        // ---------------------------------------------------------------------
        // DTOs for JSON output
        // ---------------------------------------------------------------------

        internal sealed class CityBreakdown
        {
            public int CityCount { get; set; }
            public List<CitySummaryEntry> Cities { get; set; }

            public CityBreakdown()
            {
                Cities = new List<CitySummaryEntry>();
            }
        }

        internal sealed class CitySummaryEntry
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

            // Defense (backward compatible fields)
            public int Fortification { get; set; }
            public int NumberOfPresentMilitiaUnits { get; set; }
            public bool IsBesieged { get; set; }
            public bool IsMutinous { get; set; }

            // Defense (new, richer)
            public int MainTerritoryFortificationLevel { get; set; }
            public float MainTerritoryFortificationValue { get; set; }
            public float TotalMilitiaPowerEstimation { get; set; }
            public List<TerritoryDefenseEntry> TerritoryDefense { get; set; }

            // Position
            public int DistanceWithCapital { get; set; }

            public CitySummaryEntry()
            {
                TerritoryDefense = new List<TerritoryDefenseEntry>();
            }
        }

        internal sealed class TerritoryDefenseEntry
        {
            public int TerritoryIndex { get; set; }
            public float Fortification { get; set; }
            public int FortificationLevel { get; set; }
            public int NumberOfPresentMilitiaUnits { get; set; }
            public float MilitiaPowerEstimation { get; set; }
            public List<MilitiaUnitEntry> MilitiaUnits { get; set; }

            public TerritoryDefenseEntry()
            {
                MilitiaUnits = new List<MilitiaUnitEntry>();
            }
        }

        internal sealed class MilitiaUnitEntry
        {
            public int MilitiaUnitIndex { get; set; }
            public int EmpireIndex { get; set; }

            public string UnitDefinition { get; set; }

            public float HealthRatio { get; set; }
            public float MaxHealthPoints { get; set; }
            public float HealthRegen { get; set; }
            public float HealthRegenRatio { get; set; }

            public bool IsPrimaryTarget { get; set; }

            public float AdditionalExperienceAdded { get; set; }
            public float VeterancyLevel { get; set; }
            public float Experience { get; set; }
            public float ExperienceThreshold { get; set; }

            public List<string> Abilities { get; set; }

            public MilitiaUnitEntry()
            {
                Abilities = new List<string>();
            }
        }

        // ---------------------------------------------------------------------
        // Filtering
        // ---------------------------------------------------------------------

        private static bool IsCity(SettlementInfo settlement)
        {
            return settlement.SettlementStatus == SettlementStatuses.City;
        }

        // ---------------------------------------------------------------------
        // Export entry – return DTO, no JSON, no file IO
        // ---------------------------------------------------------------------

        internal static CityBreakdown Export()
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

                var territoryMilitiaFrame = TryGetTerritoryMilitiaAndFortificationFrame(presentation);

                List<CitySummaryEntry> cities = new List<CitySummaryEntry>();

                for (int i = 0; i < count; i++)
                {
                    SettlementInfo s = settlements[i];

                    if (s.SimulationEntityGUID == 0UL)
                        continue;

                    if (!IsCity(s))
                        continue;

                    string name = s.EntityName.ToString();
                    if (string.IsNullOrEmpty(name))
                        name = "Settlement " + s.SimulationEntityGUID;

                    CitySummaryEntry entry = new CitySummaryEntry
                    {
                        EmpireIndex = s.EmpireIndex,
                        Name = name,
                        IsCapital = s.IsCapital,

                        TerritoryCount = s.TerritoryCount,
                        ExtensionDistrictsCount = s.ExtensionDistrictsCount,

                        Population = s.Population,
                        MaxPopulation = s.MaxPopulation,

                        FoodStock = (float)s.FoodStock,
                        MaxFoodStock = (float)s.MaxFoodStock,
                        FoodGainInPercent = (float)s.FoodGainInPercent,
                        TurnBeforeGrowth = (float)s.TurnBeforeGrowth,

                        ProductionNet = (float)s.ProductionNet,
                        ApprovalNetInPercent = (float)s.ApprovalNetInPercent,

                        IsBesieged = s.IsBesieged,
                        IsMutinous = s.IsMutinous,

                        DistanceWithCapital = s.DistanceWithCapital
                    };

                    entry.SettlementApprovalDisplayName =
                        TextFormatUtils.GetLocalizedNameOrNull(s.SettlementApprovalDefinitionName);

                    entry.GrowingPopulationName =
                        TextFormatUtils.GetLocalizedNameOrNull(s.GrowingPopulationName)
                        ?? s.GrowingPopulationName.ToString();

                    entry.CurrentConstructibleDisplayName =
                        TextFormatUtils.GetLocalizedNameOrNull(
                            s.CurrentConstructionInfo.ConstructibleDefinitionName
                        );

                    ResolveDefense(entry, s, territoryMilitiaFrame);

                    cities.Add(entry);
                }

                if (cities.Count == 0)
                    return null;

                return new CityBreakdown
                {
                    CityCount = cities.Count,
                    Cities = cities
                };
            }
            catch
            {
                return null;
            }
        }

        // ---------------------------------------------------------------------
        // Defense extraction (SettlementInfo + TerritoryMilitiaAndFortificationInfo)
        // ---------------------------------------------------------------------

        private static void ResolveDefense(
            CitySummaryEntry entry,
            SettlementInfo settlement,
            TerritoryMilitiaFrame territoryMilitiaFrame)
        {
            // Backward-compatible defaults from SettlementInfo (new engine still has these)
            entry.Fortification = settlement.MainTerritoryFortification;
            entry.NumberOfPresentMilitiaUnits = 0;

            entry.MainTerritoryFortificationLevel = settlement.MainTerritoryFortificationLevel;
            entry.MainTerritoryFortificationValue = 0f;
            entry.TotalMilitiaPowerEstimation = 0f;

            if (territoryMilitiaFrame == null || territoryMilitiaFrame.Length <= 0)
                return;

            var territoryIndices = settlement.TerritoryIndices;
            if (territoryIndices == null || territoryIndices.Length == 0)
                return;

            int mainTerritoryIndex = -1;
            try { mainTerritoryIndex = settlement.GetSettlementTerritoryIndex(); }
            catch { }

            for (int i = 0; i < territoryMilitiaFrame.Length; i++)
            {
                object boxed = territoryMilitiaFrame.Data.GetValue(i);
                if (boxed == null)
                    continue;

                if (!TryReadTerritoryMilitiaAndFortification(
                        boxed,
                        out int territoryIndex,
                        out float fortification,
                        out int fortificationLevel,
                        out int presentMilitiaUnits,
                        out float militiaPower,
                        out Array militiaInfosArray))
                {
                    continue;
                }

                if (!ContainsTerritoryIndex(territoryIndices, territoryIndex))
                    continue;

                entry.NumberOfPresentMilitiaUnits += presentMilitiaUnits;
                entry.TotalMilitiaPowerEstimation += militiaPower;

                var territoryEntry = new TerritoryDefenseEntry
                {
                    TerritoryIndex = territoryIndex,
                    Fortification = fortification,
                    FortificationLevel = fortificationLevel,
                    NumberOfPresentMilitiaUnits = presentMilitiaUnits,
                    MilitiaPowerEstimation = militiaPower
                };

                if (militiaInfosArray != null && militiaInfosArray.Length > 0)
                {
                    for (int m = 0; m < militiaInfosArray.Length; m++)
                    {
                        object boxedMilitia = militiaInfosArray.GetValue(m);
                        if (boxedMilitia == null)
                            continue;

                        if (!TryReadMilitiaInfo(boxedMilitia, out MilitiaUnitEntry militiaEntry))
                            continue;

                        territoryEntry.MilitiaUnits.Add(militiaEntry);
                    }
                }

                entry.TerritoryDefense.Add(territoryEntry);

                if (territoryIndex == mainTerritoryIndex)
                {
                    entry.MainTerritoryFortificationValue = fortification;
                    entry.MainTerritoryFortificationLevel = fortificationLevel;
                }
            }

            if (entry.Fortification <= 0 && entry.MainTerritoryFortificationValue > 0f)
                entry.Fortification = (int)entry.MainTerritoryFortificationValue;
        }

        private static bool ContainsTerritoryIndex(int[] territoryIndices, int territoryIndex)
        {
            for (int i = 0; i < territoryIndices.Length; i++)
            {
                if (territoryIndices[i] == territoryIndex)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Presentation frame discovery
        // ---------------------------------------------------------------------

        private sealed class TerritoryMilitiaFrame
        {
            public Array Data;
            public int Length;
        }

        private static TerritoryMilitiaFrame TryGetTerritoryMilitiaAndFortificationFrame(object presentationData)
        {
            try
            {
                if (presentationData == null)
                    return null;

                var presType = presentationData.GetType();
                var props = presType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (int p = 0; p < props.Length; p++)
                {
                    var prop = props[p];
                    var propType = prop.PropertyType;

                    bool nameMatch =
                        prop.Name.IndexOf("TerritoryMilitiaAndFortification", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool genericMatch = false;
                    if (propType.IsGenericType)
                    {
                        var args = propType.GetGenericArguments();
                        if (args != null && args.Length == 1 && args[0] == typeof(TerritoryMilitiaAndFortificationInfo))
                            genericMatch = true;
                    }

                    if (!nameMatch && !genericMatch)
                        continue;

                    object frame = prop.GetValue(presentationData, null);
                    if (frame == null)
                        continue;

                    var frameType = frame.GetType();

                    var dataMember =
                        (MemberInfo)frameType.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? frameType.GetField("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    var lengthMember =
                        (MemberInfo)frameType.GetProperty("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? frameType.GetField("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (dataMember == null || lengthMember == null)
                        continue;

                    object dataObj = ReadMemberValue(dataMember, frame);
                    object lenObj = ReadMemberValue(lengthMember, frame);

                    Array arr = dataObj as Array;
                    if (arr == null)
                        continue;

                    int len = 0;
                    if (lenObj is int iLen) len = iLen;
                    else if (lenObj != null) len = Convert.ToInt32(lenObj);

                    len = Math.Min(len, arr.Length);

                    if (len <= 0)
                        return null;

                    return new TerritoryMilitiaFrame
                    {
                        Data = arr,
                        Length = len
                    };
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static object ReadMemberValue(MemberInfo member, object instance)
        {
            if (member is PropertyInfo pi) return pi.GetValue(instance, null);
            if (member is FieldInfo fi) return fi.GetValue(instance);
            return null;
        }

        // ---------------------------------------------------------------------
        // TerritoryMilitiaAndFortificationInfo reader
        // ---------------------------------------------------------------------

        private static bool TryReadTerritoryMilitiaAndFortification(
            object boxedTerritoryInfo,
            out int territoryIndex,
            out float fortification,
            out int fortificationLevel,
            out int numberOfPresentMilitiaUnits,
            out float militiaPowerEstimation,
            out Array militiaInfos)
        {
            territoryIndex = 0;
            fortification = 0f;
            fortificationLevel = 0;
            numberOfPresentMilitiaUnits = 0;
            militiaPowerEstimation = 0f;
            militiaInfos = null;

            try
            {
                var t = boxedTerritoryInfo.GetType();

                var fTerritoryIndex = t.GetField("TerritoryIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fTerritoryIndex == null)
                    return false;

                territoryIndex = (int)fTerritoryIndex.GetValue(boxedTerritoryInfo);

                var fFort = t.GetField("Fortification", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fFort != null)
                    fortification = FixedPointToFloat(fFort.GetValue(boxedTerritoryInfo));

                var fFortLevel = t.GetField("FortificationLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fFortLevel != null)
                    fortificationLevel = (int)fFortLevel.GetValue(boxedTerritoryInfo);

                var fMilitiaCount = t.GetField("NumberOfPresentMilitiaUnits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fMilitiaCount != null)
                    numberOfPresentMilitiaUnits = (int)fMilitiaCount.GetValue(boxedTerritoryInfo);

                var fPower = t.GetField("MilitiaPowerEstimation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fPower != null)
                    militiaPowerEstimation = FixedPointToFloat(fPower.GetValue(boxedTerritoryInfo));

                var fInfos = t.GetField("MilitiaInfos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fInfos != null)
                    militiaInfos = fInfos.GetValue(boxedTerritoryInfo) as Array;

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // MilitiaInfo reader
        // ---------------------------------------------------------------------

        private static bool TryReadMilitiaInfo(object boxedMilitiaInfo, out MilitiaUnitEntry entry)
        {
            entry = null;

            try
            {
                var t = boxedMilitiaInfo.GetType();

                var fMilitiaUnitIndex = t.GetField("MilitiaUnitIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fEmpireIndex = t.GetField("EmpireIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fUnitDef = t.GetField("UnitDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (fMilitiaUnitIndex == null || fEmpireIndex == null || fUnitDef == null)
                    return false;

                entry = new MilitiaUnitEntry
                {
                    MilitiaUnitIndex = (int)fMilitiaUnitIndex.GetValue(boxedMilitiaInfo),
                    EmpireIndex = (int)fEmpireIndex.GetValue(boxedMilitiaInfo),
                    UnitDefinition = SafeStaticStringToString(fUnitDef.GetValue(boxedMilitiaInfo)),

                    HealthRatio = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "HealthRatio")),
                    MaxHealthPoints = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "MaxHealthPoints")),
                    HealthRegen = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "HealthRegen")),
                    HealthRegenRatio = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "HealthRegenRatio")),

                    IsPrimaryTarget = ReadBoolField(t, boxedMilitiaInfo, "IsPrimaryTarget"),

                    AdditionalExperienceAdded = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "AdditionalExperienceAdded")),
                    VeterancyLevel = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "VeterancyLevel")),
                    Experience = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "Experience")),
                    ExperienceThreshold = FixedPointToFloat(ReadFieldValue(t, boxedMilitiaInfo, "ExperienceThreshold"))
                };

                // Abilities is a StaticString[] with AbilitiesCount
                int abilitiesCount = ReadIntField(t, boxedMilitiaInfo, "AbilitiesCount");

                object abilitiesObj = ReadFieldValue(t, boxedMilitiaInfo, "Abilities");
                Array abilitiesArray = abilitiesObj as Array;

                if (abilitiesArray != null && abilitiesArray.Length > 0 && abilitiesCount > 0)
                {
                    int max = Math.Min(abilitiesCount, abilitiesArray.Length);
                    for (int i = 0; i < max; i++)
                    {
                        object ss = abilitiesArray.GetValue(i);
                        string ability = SafeStaticStringToString(ss);
                        if (!string.IsNullOrEmpty(ability))
                            entry.Abilities.Add(ability);
                    }
                }

                return true;
            }
            catch
            {
                entry = null;
                return false;
            }
        }

        private static object ReadFieldValue(Type t, object instance, string fieldName)
        {
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f != null ? f.GetValue(instance) : null;
        }

        private static int ReadIntField(Type t, object instance, string fieldName)
        {
            object v = ReadFieldValue(t, instance, fieldName);
            if (v is int i) return i;
            if (v == null) return 0;
            try { return Convert.ToInt32(v); } catch { return 0; }
        }

        private static bool ReadBoolField(Type t, object instance, string fieldName)
        {
            object v = ReadFieldValue(t, instance, fieldName);
            if (v is bool b) return b;
            if (v == null) return false;
            try { return Convert.ToBoolean(v); } catch { return false; }
        }

        private static string SafeStaticStringToString(object staticString)
        {
            if (staticString == null)
                return null;

            try
            {
                return staticString.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static float FixedPointToFloat(object fixedPoint)
        {
            if (fixedPoint == null)
                return 0f;

            try
            {
                if (fixedPoint is float f) return f;
                if (fixedPoint is double d) return (float)d;
                if (fixedPoint is int i) return i;

                double cd = Convert.ToDouble(fixedPoint);
                return (float)cd;
            }
            catch
            {
                try
                {
                    string s = fixedPoint.ToString();
                    if (float.TryParse(s, out float parsed))
                        return parsed;
                }
                catch { }

                return 0f;
            }
        }
    }
}