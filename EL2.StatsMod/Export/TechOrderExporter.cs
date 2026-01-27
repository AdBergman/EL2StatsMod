using System.Collections.Generic;
using Amplitude;
using Amplitude.Collections;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Tech;

namespace EL2.StatsMod.Export
{
    internal static class TechOrderExporter
    {
        /// <summary>
        /// Builds the tech order payload describing the order in which each empire unlocked technologies.
        /// This does NOT serialize to JSON and does NOT write to disk.
        /// EndGameInfoExporter is responsible for serialization policy (camelCase).
        /// </summary>
        internal static TechOrderSnapshot Export(EmpireStatistics[] allEmpiresStatistics)
        {
            if (allEmpiresStatistics == null || allEmpiresStatistics.Length == 0)
            {
                return null;
            }

            int empireCount = allEmpiresStatistics.Length;

            // Collect all entries in a simple list first.
            List<TechOrderEntry> entries = new List<TechOrderEntry>();

            for (int empireIndex = 0; empireIndex < empireCount; empireIndex++)
            {
                // ref to avoid copy, like your CSV version
                ref EmpireStatistics stats = ref allEmpiresStatistics[empireIndex];
                ListOfStruct<EmpireStatistics.UnlockedTechnologyInfo> techs = stats.EmpireTechnologyUnlocked;

                int length = techs.Length;
                for (int i = 0; i < length; i++)
                {
                    ref EmpireStatistics.UnlockedTechnologyInfo info = ref techs.Data[i];

                    int turn = info.Turn;
                    StaticString techKey = info.TechnologyName;

                    // Stable key for EWShop (definition name)
                    string definitionName = techKey.ToString();

                    // Pretty name from the game UI (if resolver available)
                    string displayName = TechNameResolver.ResolveDisplayName(techKey);

                    TechOrderEntry entry = new TechOrderEntry();
                    entry.EmpireIndex = empireIndex;
                    entry.Turn = turn;
                    entry.TechnologyDefinitionName = definitionName;
                    entry.TechnologyDisplayName = displayName;

                    entries.Add(entry);
                }
            }

            // Sort by turn first, then by empire index for deterministic output
            entries.Sort(
                delegate (TechOrderEntry a, TechOrderEntry b)
                {
                    int turnCompare = a.Turn.CompareTo(b.Turn);
                    if (turnCompare != 0)
                        return turnCompare;

                    return a.EmpireIndex.CompareTo(b.EmpireIndex);
                });

            TechOrderSnapshot snapshot = new TechOrderSnapshot();
            snapshot.EmpireCount = empireCount;
            snapshot.EntryCount = entries.Count;
            snapshot.Entries = entries.ToArray();

            return snapshot;
        }

        // --- DTOs for JSON shape ---

        /// <summary>
        /// Root object for the tech order section.
        /// </summary>
        internal sealed class TechOrderSnapshot
        {
            public int EmpireCount { get; set; }
            public int EntryCount { get; set; }
            public TechOrderEntry[] Entries { get; set; }
        }

        /// <summary>
        /// One "tech unlocked on turn X by empire Y" record.
        /// </summary>
        internal sealed class TechOrderEntry
        {
            public int EmpireIndex { get; set; }
            public int Turn { get; set; }

            /// <summary>
            /// Internal definition key (e.g. "Technology_Masonry").
            /// This is what EWShop should primarily key on.
            /// </summary>
            public string TechnologyDefinitionName { get; set; }

            /// <summary>
            /// Localized / user-facing name resolved from the game (optional but nice to have).
            /// </summary>
            public string TechnologyDisplayName { get; set; }
        }
    }
}
