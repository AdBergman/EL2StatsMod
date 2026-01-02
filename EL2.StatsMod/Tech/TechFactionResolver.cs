using System;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Data.Simulation.Prerequisites;

namespace EL2.StatsMod
{
    internal static class TechFactionResolver
    {
        /// <summary>
        /// Returns a raw / lightly cleaned faction key for a technology,
        /// based on its FactionTraitPrerequisites.
        ///
        /// Examples (output from this method):
        /// - "Aspect"
        /// - "LastLord"
        /// - "Mukag"
        ///
        /// This is ONLY used for the MajorFaction column in the CSV.
        /// </summary>
        public static string ResolveMajorFaction(TechnologyDefinition techDef)
        {
            if (techDef == null)
                return string.Empty;

            FactionTraitPrerequisite[] prereqs = techDef.FactionTraitPrerequisites;
            if (prereqs == null || prereqs.Length == 0)
                return string.Empty;

            for (int i = 0; i < prereqs.Length; i++)
            {
                var prereq = prereqs[i];

                // We only care about "Any" (must have this trait).
                if (prereq.Operator != FactionTraitPrerequisite.Operators.Any)
                    continue;

                DatatableElementReference traitRef = prereq.FactionTrait;

                // Get the underlying StaticString name of the referenced trait
                StaticString name = traitRef.ElementName;
                if (StaticString.IsNullOrEmpty(name))
                    continue;

                string raw = name.ToString(); // e.g. "FactionAffinity_Aspect" or "FactionTrait_LastLord_Units"
                return CleanMajorFactionKey(raw);
            }

            return string.Empty;
        }

        /// <summary>
        /// Very small, explicit clean-up for faction keys.
        /// No generic heuristics, no title/description cleanup – only the MajorFaction field.
        /// 
        /// Handles:
        /// - "FactionAffinity_Aspect" -> "Aspect"
        /// - "FactionTrait_LastLord_Units" -> "LastLord"
        /// Otherwise returns the raw key.
        /// </summary>
        private static string CleanMajorFactionKey(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            const string affinityPrefix = "FactionAffinity_";
            const string traitPrefix = "FactionTrait_";

            // Case 1: "FactionAffinity_Aspect"
            if (raw.StartsWith(affinityPrefix, StringComparison.Ordinal))
            {
                return raw.Substring(affinityPrefix.Length);
            }

            // Case 2: "FactionTrait_LastLord_Units"
            if (raw.StartsWith(traitPrefix, StringComparison.Ordinal))
            {
                string withoutPrefix = raw.Substring(traitPrefix.Length);

                // If there is an underscore, take only the part before it ("LastLord")
                int underscoreIndex = withoutPrefix.IndexOf('_');
                if (underscoreIndex > 0)
                    return withoutPrefix.Substring(0, underscoreIndex);

                return withoutPrefix;
            }

            // Default: return as-is (no stripping, no guessing)
            return raw;
        }
    }
}
