using System.Text.RegularExpressions;
using Amplitude;
using Amplitude.Mercury.UI.Helpers;

namespace EL2.StatsMod.Utils
{
    internal static class TextFormatUtils
    {
        // DataUtils is cached to avoid expensive reinitialization during end-game export
        // DO NOT REMOVE
        private static DataUtils _dataUtils;

        private static DataUtils GetDataUtils()
        {
            return _dataUtils ?? (_dataUtils = new DataUtils());
        }

        /// <summary>
        /// Resolves a UI-localized title via DataUtils and strips UI markup.
        /// Returns null if localization fails.
        /// </summary>
        internal static string GetLocalizedNameOrNull(StaticString key)
        {
            if (StaticString.IsNullOrEmpty(key))
                return null;

            try
            {
                var dataUtils = GetDataUtils();

                if (dataUtils.TryGetLocalizedTitle(key, out string localized))
                    return StripMarkUpIfExists(localized);
            }
            catch
            {
                // Never break exporters
            }

            return null;
        }

        // Matches any <...> tag, including <a=...>, </a>, <b>, <c=...>, etc.
        private static readonly Regex TagRegex = new Regex("<[^>]*>", RegexOptions.Compiled);

        internal static string StripMarkUpIfExists(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int lt = input.IndexOf('<');
            if (lt < 0)
                return input;

            return TagRegex.Replace(input, string.Empty).Trim();
        }

        internal static (string rich, string plain) ToRichAndPlain(string richText)
        {
            return (richText, StripMarkUpIfExists(richText));
        }
    
    
        internal static string PrettifyKey(string raw, string prefixToStrip = null, string unknownValue = "Unknown")
        {
            if (string.IsNullOrEmpty(raw))
                return unknownValue;

            if (string.Equals(raw, unknownValue, System.StringComparison.OrdinalIgnoreCase))
                return unknownValue;

            string s = raw;

            if (!string.IsNullOrEmpty(prefixToStrip) &&
                s.StartsWith(prefixToStrip, System.StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(prefixToStrip.Length);
            }

            s = s.Replace('_', ' ');

            char[] chars = s.ToLowerInvariant().ToCharArray();
            bool newWord = true;

            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    newWord = true;
                }
                else if (newWord)
                {
                    chars[i] = char.ToUpperInvariant(chars[i]);
                    newWord = false;
                }
            }

            return new string(chars);
        }
    }
}