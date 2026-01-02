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
        
        internal static string LocalizeOrPrettifyOrRaw(string value, string unknownValue = "Unknown")
        {
            if (string.IsNullOrEmpty(value))
                return unknownValue;

            // Try localization first
            try
            {
                var key = new StaticString(value);
                var localized = GetLocalizedNameOrNull(key);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            catch
            {
                // ignore
            }

            // Fallback: prettify keys like WorldSize_Large
            var pretty = PrettifyKey(value);
            if (!string.IsNullOrEmpty(pretty))
                return pretty;

            return value;
        }
        
        internal static string LocalizeOrRaw(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            // Try localizing if it's a key
            try
            {
                var key = new StaticString(value);
                var localized = GetLocalizedNameOrNull(key);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            catch
            {
                // ignore - might not be a valid StaticString key
            }

            // fallback: unchanged
            return value;
        }

    }
}