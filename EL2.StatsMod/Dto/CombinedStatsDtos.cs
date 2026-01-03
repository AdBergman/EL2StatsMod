using System.Collections.Generic;

namespace EL2.StatsMod.Dto
{
    // Root object for CombinedStatsExporter JSON
    internal sealed class AllStatsRoot
    {
        public string Version;
        public string GeneratedAtUtc;
        public string GameId;

        public int EmpireCount;
        public int MaxTurn;
        public int TopScoreEmpire;
        public float TopScore;

        public GameSettings Game;
        public VictorySettings Victory;

        public List<EmpireStatsEntry> Empires;
    }

    internal sealed class GameSettings
    {
        public string Difficulty;
        public string MapSize;
        public string GameSpeed;
    }

    internal sealed class VictorySettings
    {
        public string VictoryPreset;
        public string ActualVictoryCondition;
        public string VictoryConditionsEnabled;
    }

    internal sealed class EmpireStatsEntry
    {
        public int EmpireIndex;

        public string FactionKey;          // e.g. "Faction_Necrophage"
        public string FactionDisplayName;  // e.g. "Necrophage"

        public int TechCount;
        public int FinalEraIndex;
        public int[] FirstTurnPerEra;      // clean, per-era first turn (0 if never reached / invalid)

        public List<TurnStatsEntry> PerTurn;
    }

    internal sealed class TurnStatsEntry
    {
        public int Turn;

        public float Food;
        public float Industry;
        public float Dust;
        public float Science;
        public float Influence;
        public float Approval;
        public float Populations;
        public float Technologies;
        public float Units;
        public float Cities;
        public float Territories;
        public float Score;
    }
}