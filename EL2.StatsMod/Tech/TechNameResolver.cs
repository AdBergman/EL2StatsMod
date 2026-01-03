using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Amplitude;
using Amplitude.Mercury.UI.Tooltips;

namespace EL2.StatsMod.Tech
{
    internal static class TechNameResolver
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

        // Very simple regex: removes any `<...>` tag blocks
        // e.g. `<a=Tech_Key>Scavenging</a>` -> `Scavenging`
        private static readonly Regex TagRegex = new Regex("<.*?>", RegexOptions.Compiled);

        internal static string ResolveDisplayName(StaticString techKey)
        {
            if (StaticString.IsNullOrEmpty(techKey))
                return string.Empty;

            string key = techKey.ToString();

            string cached;
            if (Cache.TryGetValue(key, out cached))
                return cached;

            string result = key; // fallback to internal key

            try
            {
                var tad = new TitleAndDescription(techKey);

                if (!string.IsNullOrEmpty(tad.Title))
                {
                    result = SanitizeTitle(tad.Title);
                }
            }
            catch (Exception ex)
            {
                StatsLoggerPlugin.Log?.LogWarning(
                    string.Format("[EL2 Stats] Failed to resolve tech title for '{0}': {1}", key, ex.Message));
            }

            Cache[key] = result;
            return result;
        }

        private static string SanitizeTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            // Remove all `<...>` blocks
            string withoutTags = TagRegex.Replace(raw, string.Empty);

            // Trim whitespace just in case
            return withoutTags.Trim();
        }
    }
}