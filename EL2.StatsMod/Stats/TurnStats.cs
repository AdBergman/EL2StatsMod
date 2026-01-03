namespace EL2.StatsMod.Stats
{
    // Holds all stats we care about for a single (empire, turn)
    internal struct TurnStats
    {
        public float? Food;
        public float? Industry;
        public float? Dust;
        public float? Science;
        public float? Influence;

        public float? Approval;
        public float? Populations;
        public float? Technologies;
        public float? Units;
        public float? Cities;
        public float? Territories;

        public float? Score;
    }
}