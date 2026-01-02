using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace EL2.StatsMod
{
    [BepInPlugin("ab.el2.statsmod", "EL2 Stats Mod", "0.1.0")]
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