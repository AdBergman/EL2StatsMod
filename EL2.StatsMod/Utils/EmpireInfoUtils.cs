using System;
using System.Reflection;
using Amplitude;
using Amplitude.Mercury.Interop;

namespace EL2.StatsMod.Utils
{
    internal static class EmpireInfoUtils
    {
        // --------------------------------------------------------------------
        // Existing helpers copied from your original exporter
        // --------------------------------------------------------------------

        /// <summary>
        /// Best-effort attempt to get the major faction name for a given empire index.
        /// 1) Try GameSnapshot.PresentationData.EmpireInfo[...] via reflection (field with "Faction" in name).
        /// 2) Fallback to Sandbox.MajorEmpires as before.
        /// Returns "Unknown" if anything fails.
        /// </summary>
        
        internal static string ResolveEmpireFactionName(int empireIndex)
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