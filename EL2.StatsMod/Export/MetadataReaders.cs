using Amplitude;
using Amplitude.Framework;
using Amplitude.Framework.Session;
using Amplitude.Mercury.Analytics;
using Amplitude.Mercury.Interop;
using EL2.StatsMod.Dto;
using EL2.StatsMod.Utils;

using MercuryMetadataKeys = Amplitude.Mercury.Session.MetadataKeys;

namespace EL2.StatsMod.Export
{
    internal static class MetadataReaders
    {
        internal static GameSettings ReadGameSettingsMetadata()
        {
            GameSettings game = new GameSettings
            {
                Difficulty = "Unknown",
                MapSize = "Unknown",
                GameSpeed = "Unknown"
            };

            try
            {
                var sessionService = Services.GetService<ISessionService>();
                var metadata = sessionService != null ? sessionService.Session.Metadata : null;

                if (metadata != null)
                {
                    string value;

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.GameDifficulty, out value))
                        game.Difficulty = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.WorldSize, out value))
                        game.MapSize = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";

                    if (metadata.TryGetMetadata(MercuryMetadataKeys.GameSpeed, out value))
                        game.GameSpeed = TextFormatUtils.LocalizeOrRaw(value) ?? "Unknown";
                }
            }
            catch (System.Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning(
                    "[MetadataReaders] Failed to read game settings metadata: " + ex.Message
                );
            }

            return game;
        }

        internal static VictorySettings ReadVictorySettingsMetadata()
        {
            VictorySettings victory = new VictorySettings
            {
                VictoryPreset = "Unknown",
                ActualVictoryCondition = "Unknown",
                VictoryConditionsEnabled = "Unknown"
            };

            try
            {
                var vcSnapshot = Snapshots.VictoryConditionsWindowSnapshot;
                if (vcSnapshot != null)
                {
                    var data = vcSnapshot.PresentationData;

                    // Preset chosen in game setup
                    StaticString defName = data.EndGameDefinitionName;
                    if (!StaticString.IsNullOrEmpty(defName))
                        victory.VictoryPreset = defName.ToString();

                    // Actual victory condition that fired
                    victory.ActualVictoryCondition = data.EndGameConditionType.ToString();

                    // Enabled victory conditions
                    try
                    {
                        string detail = AnalyticsEvent_GameCreated.GatherEndGameConditionActivation();
                        if (!string.IsNullOrEmpty(detail))
                            victory.VictoryConditionsEnabled = detail;
                    }
                    catch
                    {
                        // Fallback: build from EndGameConditionsInfo
                        var infos = data.EndGameConditionsInfo;
                        if (infos != null && infos.Length > 0)
                        {
                            System.Text.StringBuilder sb = new System.Text.StringBuilder();
                            for (int i = 0; i < infos.Length; i++)
                            {
                                if (i > 0)
                                    sb.Append(';');

                                var info = infos[i];
                                sb.Append(info.ConditionType);
                                sb.Append(':');
                                sb.Append(info.IsEnabled ? "True" : "False");
                            }

                            victory.VictoryConditionsEnabled = sb.ToString();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning(
                    "[MetadataReaders] Failed to read victory settings metadata: " + ex.Message
                );
            }

            return victory;
        }
    }
}
