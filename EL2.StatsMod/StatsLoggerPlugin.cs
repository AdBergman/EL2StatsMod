using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace EL2.StatsMod
{
    [BepInPlugin("ab.el2.statsmod", "EL2 Stats Mod", "1.1.0")]
    // Entry point for the EL2 Stats Mod.
    //
    // Responsibilities:
    // - Initialize shared logging
    // - Install Harmony patches (hooks into EL2)
    // - Coordinate end-game export to JSON
    //
    // Harmony patch classes live in EL2.StatsMod.Patches.
    // Export logic lives in EL2.StatsMod.Export.

    public class StatsLoggerPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _harmony = new Harmony("ab.el2.statsmod");
            _harmony.PatchAll();

            Log.LogInfo("EL2 Stats Mod loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Log.LogInfo("EL2 Stats Mod unloaded.");
        }
    }
}