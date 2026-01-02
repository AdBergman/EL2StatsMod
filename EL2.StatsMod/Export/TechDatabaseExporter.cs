using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.UI.Tooltips;
using BepInEx;
using Path = System.IO.Path;

namespace EL2.StatsMod
{
    internal static class TechDatabaseExporter
    {
        /// <summary>
        /// Dumps all technologies visible in the TechnologyScreenSnapshot into a CSV.
        /// One row per (tech, unlock entry).
        ///
        /// CSV schema:
        /// TechName,EraIndex,ResearchCost,MajorFaction,UnlockIndex,UnlockType,UnlockCategory,Amount,UnlockKey,UnlockKind,EffectTitle,EffectDescription
        /// </summary>
        internal static void DumpAllTechnologiesCsv(string timestamp)
        {
            try
            {
                var techSnapshot = Snapshots.TechnologyScreenSnapshot.PresentationData;
                TechnologyInfo[] allTechInfos = techSnapshot.TechnologyInfo;

                if (allTechInfos == null || allTechInfos.Length == 0)
                {
                    StatsLoggerPlugin.Log?.LogWarning("[EL2 Stats] TechDatabaseExporter: No TechnologyInfo entries found in snapshot.");
                    return;
                }

                // Try to grab the TechnologyDefinition database once
                IDatabase<TechnologyDefinition> techDb = null;
                try
                {
                    techDb = Databases.GetDatabase<TechnologyDefinition>();
                }
                catch (Exception ex)
                {
                    StatsLoggerPlugin.Log?.LogWarning("[EL2 Stats] Failed to get TechnologyDefinition DB: " + ex.Message);
                }

                var culture = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();

                sb.AppendLine("# EL2 Technology Database Dump");
                sb.AppendLine("# ExportVersion=6");
                sb.AppendLine("# GeneratedAt=" + DateTime.Now.ToString("o", culture));
                sb.AppendLine("# TechCountApprox=" + allTechInfos.Length.ToString(culture));
                sb.AppendLine("TechName,EraIndex,ResearchCost,MajorFaction,UnlockIndex,UnlockType,UnlockCategory,Amount,UnlockKey,UnlockKind,EffectTitle,EffectDescription");

                // Some TechnologyInfo entries may repeat; process each tech key only once.
                var processed = new HashSet<string>();

                for (int i = 0; i < allTechInfos.Length; i++)
                {
                    TechnologyInfo info = allTechInfos[i];

                    StaticString techKey = info.TechnologyDefinitionName;
                    string techKeyString = techKey.ToString();

                    if (processed.Contains(techKeyString))
                        continue;

                    processed.Add(techKeyString);

                    string techName = TechNameResolver.ResolveDisplayName(techKey);
                    int eraIndex = info.TechnologyEraIndex;
                    float researchCost = (float)info.ResearchCost;

                    // Resolve major faction (raw trait/affinity key from FactionTraitPrerequisites, if any)
                    string majorFactionRaw = string.Empty;
                    if (techDb != null)
                    {
                        try
                        {
                            TechnologyDefinition techDef = techDb.GetValue(techKey);
                            if (techDef != null)
                            {
                                majorFactionRaw = TechFactionResolver.ResolveMajorFaction(techDef) ?? string.Empty;
                            }
                        }
                        catch
                        {
                            majorFactionRaw = string.Empty;
                        }
                    }

                    string majorFaction = MajorFactionFormatter.Normalize(majorFactionRaw);

                    UnlockInfo[] unlocks = info.UnlockInfo;

                    if (unlocks == null || unlocks.Length == 0)
                    {
                        // Tech without explicit unlock info
                        WriteCsvLine(
                            sb,
                            techName,
                            eraIndex,
                            researchCost,
                            majorFaction,
                            -1,
                            "None",
                            "None",
                            0f,
                            string.Empty,       // UnlockKey
                            string.Empty,       // UnlockKind
                            "No unlock info",
                            string.Empty,
                            culture);
                    }
                    else
                    {
                        for (int unlockIndex = 0; unlockIndex < unlocks.Length; unlockIndex++)
                        {
                            UnlockInfo unlock = unlocks[unlockIndex];

                            string unlockType = unlock.UnlockType.ToString();
                            string unlockCategory = unlock.UnlockCategory.ToString();
                            float amount = (float)unlock.Amount;

                            // Raw unlock key from the game data
                            string unlockKey = unlock.UnlockElementName.ToString();
                            string unlockKind = UnlockKindFormatter.DeriveUnlockKind(unlockKey);

                            string effectTitle;
                            string effectDescription;
                            ResolveUnlockText(ref unlock, out effectTitle, out effectDescription);

                            WriteCsvLine(
                                sb,
                                techName,
                                eraIndex,
                                researchCost,
                                majorFaction,
                                unlockIndex,
                                unlockType,
                                unlockCategory,
                                amount,
                                unlockKey,
                                unlockKind,
                                effectTitle,
                                effectDescription,
                                culture);
                        }
                    }
                }

                var fileName = "EL2_TechDatabase_" + timestamp + ".csv";
                var filePath = Path.Combine(Paths.BepInExRootPath, fileName);

                File.WriteAllText(filePath, sb.ToString());
                StatsLoggerPlugin.Log?.LogInfo("[EL2 Stats] Saved tech database dump to " + filePath);
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogError("[EL2 Stats] TechDatabaseExporter: Unhandled exception: " + ex);
            }
        }

        private static void ResolveUnlockText(
            ref UnlockInfo unlock,
            out string effectTitle,
            out string effectDescription)
        {
            effectTitle = string.Empty;
            effectDescription = string.Empty;

            // Prefer UIMapperOverrideName if present, otherwise fall back to UnlockElementName.
            StaticString mapperName = unlock.UIMapperOverrideName;
            if (StaticString.IsNullOrEmpty(mapperName))
            {
                mapperName = unlock.UnlockElementName;
            }

            if (StaticString.IsNullOrEmpty(mapperName))
            {
                effectTitle = "Unknown";
                effectDescription = string.Empty;
                return;
            }

            try
            {
                var tad = new TitleAndDescription(mapperName);

                string rawTitle = string.IsNullOrEmpty(tad.Title)
                    ? mapperName.ToString()
                    : tad.Title;

                string rawDescription = tad.Description ?? string.Empty;

                effectTitle = CleanEffectTitle(rawTitle);
                effectDescription = CleanEffectDescription(rawDescription);
            }
            catch (Exception ex)
            {
                string fallback = mapperName.ToString();
                effectTitle = CleanEffectTitle(fallback);
                effectDescription = "Failed to resolve UI mapper: " + ex.Message;
            }
        }

        /// <summary>
        /// Make EffectTitle readable while keeping semantics.
        /// - If it's "&lt;a=Something&gt;Name&lt;/a&gt;", keep "Name".
        /// - If it's "%Some_Key_Title", turn into "Some Key".
        /// - Otherwise, trim.
        /// </summary>
        private static string CleanEffectTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            raw = raw.Trim();

            // Percent-key style: %Something_Title
            if (raw.StartsWith("%", StringComparison.Ordinal))
            {
                return CleanPercentTitle(raw);
            }

            // Anchor style: <a=Something>Text</a>
            if (raw.IndexOf("<a=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string inner = ExtractAnchorInnerText(raw);
                if (!string.IsNullOrEmpty(inner))
                    return inner;
            }

            return raw;
        }

        private static string CleanPercentTitle(string raw)
        {
            // raw starts with '%'
            string value = raw.Substring(1); // drop leading '%'

            // Drop trailing Title/Description if present
            if (value.EndsWith("Title", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - "Title".Length);
            else if (value.EndsWith("Description", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - "Description".Length);

            // Replace underscores with spaces; keep tokens (Effect, EmpireBonus, etc.)
            value = value.Replace('_', ' ');

            return value.Trim();
        }

        private static string ExtractAnchorInnerText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            int lt = raw.IndexOf('<');
            if (lt < 0)
                return raw.Trim();

            int gt = raw.IndexOf('>', lt + 1);
            if (gt < 0)
                return raw.Trim();

            int close = raw.IndexOf("</a>", gt + 1, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                return raw.Trim();

            int length = close - (gt + 1);
            if (length <= 0)
                return string.Empty;

            return raw.Substring(gt + 1, length).Trim();
        }

        private static string CleanEffectDescription(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            // For now just normalize whitespace; keep tags/keys if any.
            raw = raw.Replace("\r", " ").Replace("\n", " ");
            return raw.Trim();
        }

        private static string SanitizeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Avoid breaking CSV structure
            value = value.Replace("\r", " ").Replace("\n", " ");
            value = value.Replace(",", ";");
            return value.Trim();
        }

        private static void WriteCsvLine(
            StringBuilder sb,
            string techName,
            int eraIndex,
            float researchCost,
            string majorFaction,
            int unlockIndex,
            string unlockType,
            string unlockCategory,
            float amount,
            string unlockKey,
            string unlockKind,
            string effectTitle,
            string effectDescription,
            CultureInfo culture)
        {
            sb.Append(SanitizeCsvField(techName)).Append(',');
            sb.Append(eraIndex.ToString(culture)).Append(',');
            sb.Append(researchCost.ToString(culture)).Append(',');
            sb.Append(SanitizeCsvField(majorFaction ?? string.Empty)).Append(',');
            sb.Append(unlockIndex.ToString(culture)).Append(',');
            sb.Append(SanitizeCsvField(unlockType)).Append(',');
            sb.Append(SanitizeCsvField(unlockCategory)).Append(',');
            sb.Append(amount.ToString(culture)).Append(',');
            sb.Append(SanitizeCsvField(unlockKey ?? string.Empty)).Append(',');
            sb.Append(SanitizeCsvField(unlockKind ?? string.Empty)).Append(',');
            sb.Append(SanitizeCsvField(effectTitle ?? string.Empty)).Append(',');
            sb.Append(SanitizeCsvField(effectDescription ?? string.Empty)).AppendLine();
        }
    }

    internal static class MajorFactionFormatter
    {
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            switch (raw)
            {
                case "Aspect":
                case "Aspects":
                    return "Aspects";

                case "LastLord":
                    return "Lords";

                case "Mukag":
                    return "Tahuks";

                case "Necrophage":
                    return "Necrophages";

                case "KinOfSheredyn":
                    return "Kin";

                default:
                    return raw;
            }
        }
    }

    internal static class UnlockKindFormatter
    {
        public static string DeriveUnlockKind(string unlockKey)
        {
            if (string.IsNullOrEmpty(unlockKey))
                return string.Empty;

            if (unlockKey.StartsWith("Unit_", StringComparison.Ordinal))
                return "UnitSpecialization";

            if (unlockKey.StartsWith("DistrictImprovement_", StringComparison.Ordinal))
                return "DistrictImprovement";

            if (unlockKey.StartsWith("Necrophage_", StringComparison.Ordinal) ||
                unlockKey.StartsWith("LastLord_", StringComparison.Ordinal) ||
                unlockKey.StartsWith("Mukag_", StringComparison.Ordinal) ||
                unlockKey.StartsWith("KinOfSheredyn_", StringComparison.Ordinal) ||
                unlockKey.StartsWith("Aspect_", StringComparison.Ordinal))
                return "FactionUnlock";

            if (unlockKey.StartsWith("ArmyActionType", StringComparison.Ordinal))
                return "ArmyAction";

            if (unlockKey.StartsWith("Converter_", StringComparison.Ordinal))
                return "Converter";

            return "Other";
        }
    }
}
