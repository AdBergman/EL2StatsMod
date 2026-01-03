using System.Collections.Generic;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Dto;
using EL2.StatsMod.Utils;

namespace EL2.StatsMod.Export
{
    internal static class EmpireStatsEntryFactory
    {
        internal static EmpireStatsEntry Build(
            int empireIndex,
            ref EmpireStatistics stats,
            int maxTurn)
        {
            EmpireStatsEntry entry = new EmpireStatsEntry();
            entry.EmpireIndex = empireIndex;

            string factionKey = EmpireInfoUtils.ResolveEmpireFactionName(empireIndex);
            entry.FactionKey = factionKey;
            entry.FactionDisplayName = TextFormatUtils.PrettifyKey(factionKey, "Faction_");

            // Tech count
            entry.TechCount = stats.EmpireTechnologyUnlocked.Length;

            // Era timing logic
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
                            cleaned = 1;

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
                            cleaned = 0;
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

            // Allocate list for per-turn stats (filled later)
            entry.PerTurn = new List<TurnStatsEntry>();

            return entry;
        }
    }
}
