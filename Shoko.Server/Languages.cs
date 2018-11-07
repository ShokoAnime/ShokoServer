using System.Collections.Generic;
using Shoko.Server.Settings;

namespace Shoko.Server
{
    public class Languages
    {
        public static string[] AllLanguages
        {
            get
            {
                string[] lans = new string[]
                {
                    "EN", "X-JAT", "JA", "AR", "BD", "BG", "CA", "CS", "CZ", "DA", "DK", "DE", "EL", "ES", "ET", "FI",
                    "FR", "GL", "GR", "HE", "HU", "IL", "IT", "KO", "LT", "MN", "MS", "MY", "NL", "NO", "PL", "PT",
                    "PT-BR", "RO", "RU", "SK", "SL", "SR", "SV", "SE", "TH", "TR", "UK", "UA", "VI", "ZH", "ZH-HANS",
                    "ZH-HANT"
                };
                return lans;
            }
        }

        public static List<NamingLanguage> AllNamingLanguages
        {
            get
            {
                List<NamingLanguage> lans = new List<NamingLanguage>();

                foreach (string lan in Languages.AllLanguages)
                {
                    NamingLanguage nlan = new NamingLanguage(lan);
                    lans.Add(nlan);
                }

                return lans;
            }
        }

        public static string GetFlagImage(string language)
        {
            switch (language.Trim().ToUpper())
            {
                case "EN":
                    return @"/Images/Flags/uk_unitedkingdom.gif";
                case "X-JAT":
                    return @"/Images/Flags/jp.gif";
                case "JA":
                    return @"/Images/Flags/jp.gif";
                case "AR":
                    return @"/Images/Flags/ar.gif"; // Arabic
                case "BD":
                    return @"/Images/Flags/bd.gif"; // Bangladesh
                case "BG":
                    return @"/Images/Flags/bg.gif"; // Bulgarian
                case "CA":
                    return @"/Images/Flags/ca.gif"; // Canadian
                case "CS":
                    return @"/Images/Flags/cz.gif"; // Czech
                case "CZ":
                    return @"/Images/Flags/cz.gif"; // Czech
                case "DA":
                    return @"/Images/Flags/dk.gif"; // Danish
                case "DK":
                    return @"/Images/Flags/dk.gif"; // Danish
                case "DE":
                    return @"/Images/Flags/de_germany.gif"; // German
                case "EL":
                    return @"/Images/Flags/gr.gif"; // Greek
                case "ES":
                    return @"/Images/Flags/es.gif"; // Spanish
                case "ET":
                    return @"/Images/Flags/et.gif"; // Estonian
                case "FI":
                    return @"/Images/Flags/fi.gif"; // Finnish
                case "FR":
                    return @"/Images/Flags/fr.gif"; // french
                case "GL":
                    return @"/Images/Flags/gl.gif"; // Galician
                case "GR":
                    return @"/Images/Flags/gr.gif"; // Greek
                case "HE":
                    return @"/Images/Flags/il.gif"; // Hebrew
                case "HU":
                    return @"/Images/Flags/hu.gif"; // Hungarian
                case "IL":
                    return @"/Images/Flags/il.gif"; // Hebrew
                case "IT":
                    return @"/Images/Flags/it.gif"; // Italy
                case "KO":
                    return @"/Images/Flags/ko.gif"; // Korean
                case "LT":
                    return @"/Images/Flags/lt.gif"; // Lithuanian
                case "MN":
                    return @"/Images/Flags/mn.gif"; // Mongolian
                case "MS":
                    return @"/Images/Flags/my.gif"; // Malaysian
                case "MY":
                    return @"/Images/Flags/my.gif"; // Malaysian
                case "NL":
                    return @"/Images/Flags/nl.gif"; // Dutch
                case "NO":
                    return @"/Images/Flags/no.gif"; // Norwegian
                case "PL":
                    return @"/Images/Flags/pl.gif"; // Polish
                case "PT":
                    return @"/Images/Flags/pt.gif"; // Portuguese
                case "PT-BR":
                    return @"/Images/Flags/br.gif"; // Portuguese - Brazil
                case "RO":
                    return @"/Images/Flags/ro.gif"; // Romanian
                case "RU":
                    return @"/Images/Flags/ru.gif"; // Russian
                case "SK":
                    return @"/Images/Flags/sk.gif"; // Slovak
                case "SL":
                    return @"/Images/Flags/sl.gif"; // Slovenian
                case "SR":
                    return @"/Images/Flags/sr.gif"; // Serbian
                case "SV":
                    return @"/Images/Flags/se.gif"; // Swedish
                case "SE":
                    return @"/Images/Flags/se.gif"; // Swedish
                case "TH":
                    return @"/Images/Flags/th.gif"; // Thai
                case "TR":
                    return @"/Images/Flags/tr.gif"; // Turkish
                case "UK":
                    return @"/Images/Flags/ua.gif"; // Ukrainian
                case "UA":
                    return @"/Images/Flags/ua.gif"; // Ukrainian
                case "VI":
                    return @"/Images/Flags/vi.gif"; // Vietnamese
                case "ZH":
                    return @"/Images/Flags/cn.gif"; // Chinese
                case "ZH-HANS":
                    return @"/Images/Flags/cn.gif"; // Chinese (Simplified)
                case "ZH-HANT":
                    return @"/Images/Flags/cn.gif"; // Chinese (Traditional)
                default:
                    return @"/Images/16_warning.png";
            }
        }

        public static string GetLanguageDescription(string language)
        {
            switch (language.Trim().ToUpper())
            {
                case "EN":
                    return "English (en)";
                case "X-JAT":
                    return "Romaji (x-jat)";
                case "JA":
                    return "Kanji";
                case "AR":
                    return "Arabic (ar)";
                case "BD":
                    return "Bangladesh (bd)";
                case "BG":
                    return "Bulgarian (bd)";
                case "CA":
                    return "Canadian-French (ca)";
                case "CS":
                    return "Czech (cs)";
                case "CZ":
                    return "Czech (cz)";
                case "DA":
                    return "Danish (da)";
                case "DK":
                    return "Danish (dk)";
                case "DE":
                    return "German (de)";
                case "EL":
                    return "Greek (el)";
                case "ES":
                    return "Spanish (es)";
                case "ET":
                    return "Estonian (et)";
                case "FI":
                    return "Finnish (fi)";
                case "FR":
                    return "French (fr)";
                case "GL":
                    return "Galician (gl)";
                case "GR":
                    return "Greek (gr)";
                case "HE":
                    return "Hebrew (he)";
                case "HU":
                    return "Hungarian (hu)";
                case "IL":
                    return "Hebrew (il)";
                case "IT":
                    return "Italian (it)";
                case "KO":
                    return "Korean (ko)";
                case "LT":
                    return "Lithuanian (lt)";
                case "MN":
                    return "Mongolian (mn)";
                case "MS":
                    return "Malaysian (ms)";
                case "MY":
                    return "Malaysian (my)";
                case "NL":
                    return "Dutch (nl)";
                case "NO":
                    return "Norwegian (no)";
                case "PL":
                    return "Polish (pl)";
                case "PT":
                    return "Portuguese (pt)";
                case "PT-BR":
                    return "Portuguese - Brazil (pt-br)";
                case "RO":
                    return "Romanian (ro)";
                case "RU":
                    return "Russian (ru)";
                case "SK":
                    return "Slovak (sk)";
                case "SL":
                    return "Slovenian (sl)";
                case "SR":
                    return "Serbian (sr)";
                case "SV":
                    return "Swedish (sv)";
                case "SE":
                    return "Swedish (se)";
                case "TH":
                    return "Thai (th)";
                case "TR":
                    return "Turkish (tr)";
                case "UK":
                    return "Ukrainian (uk)";
                case "UA":
                    return "Ukrainian (ua)";
                case "VI":
                    return "Vietnamese (vi)";
                case "ZH":
                    return "Chinese";
                case "ZH-HANS":
                    return "Chinese (zh-hans)";
                case "ZH-HANT":
                    return "Chinese (zh-hant)";
                default:
                    return language;
            }
        }

        public static List<NamingLanguage> PreferredNamingLanguages
        {
            get
            {
                List<NamingLanguage> lans = new List<NamingLanguage>();

                foreach (string lan in ServerSettings.Instance.LanguagePreference ?? new string[] {})
                {
                    if (string.IsNullOrEmpty(lan)) continue;
                    if (lan.Trim().Length < 2) continue;
                    NamingLanguage nlan = new NamingLanguage(lan);
                    lans.Add(nlan);
                }

                return lans;
            }
        }

        public static List<NamingLanguage> PreferredEpisodeNamingLanguages
        {
            get
            {
                List<NamingLanguage> lans = new List<NamingLanguage>();

                string[] slans = ServerSettings.Instance.EpisodeLanguagePreference?.Split(',') ?? new string[] {};

                foreach (string lan in slans)
                {
                    if (string.IsNullOrEmpty(lan)) continue;
                    if (lan.Trim().Length < 2) continue;
                    NamingLanguage nlan = new NamingLanguage(lan);
                    lans.Add(nlan);
                }

                return lans;
            }
        }
    }
}
