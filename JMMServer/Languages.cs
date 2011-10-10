using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer
{
	public class Languages
	{
		public static string[] AllLanguages
		{
			get
			{
				string[] lans = new string[] { "EN", "X-JAT", "JA", "AR", "BD", "BG", "CA", "CS", "CZ"
				, "DA", "DK", "DE", "EL", "ES", "ET", "FI", "FR", "GL", "GR", "HE", "HU", "IL", "IT"
				, "KO", "LT", "MN", "MS", "MY", "NL", "NO", "PL", "PT", "PT-BR", "RO", "RU", "SK", "SL"
				, "SR", "SV", "SE", "TH", "TR", "UK", "UA", "VI", "ZH", "ZH-HANS", "ZH-HANT"};
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

		public static string GetLanguageDescription(string language)
		{
			switch (language.Trim().ToUpper())
			{
				case "EN": return "English (en)";
				case "X-JAT": return "Romaji (x-jat)";
				case "JA": return "Kanji";
				case "AR": return "Arabic (ar)";
				case "BD": return "Bangladesh (bd)";
				case "BG": return "Bulgarian (bd)";
				case "CA": return "Canadian-French (ca)";
				case "CS": return "Czech (cs)";
				case "CZ": return "Czech (cz)";
				case "DA": return "Danish (da)";
				case "DK": return "Danish (dk)";
				case "DE": return "German (de)";
				case "EL": return "Greek (el)";
				case "ES": return "Spanish (es)";
				case "ET": return "Estonian (et)";
				case "FI": return "Finnish (fi)";
				case "FR": return "French (fr)";
				case "GL": return "Galician (gl)";
				case "GR": return "Greek (gr)";
				case "HE": return "Hebrew (he)";
				case "HU": return "Hungarian (hu)";
				case "IL": return "Hebrew (il)";
				case "IT": return "Italian (it)";
				case "KO": return "Korean (ko)";
				case "LT": return "Lithuanian (lt)";
				case "MN": return "Mongolian (mn)";
				case "MS": return "Malaysian (ms)";
				case "MY": return "Malaysian (my)";
				case "NL": return "Dutch (nl)";
				case "NO": return "Norwegian (no)";
				case "PL": return "Polish (pl)";
				case "PT": return "Portuguese (pt)";
				case "PT-BR": return "Portuguese - Brazil (pt-br)";
				case "RO": return "Romanian (ro)";
				case "RU": return "Russian (ru)";
				case "SK": return "Slovak (sk)";
				case "SL": return "Slovenian (sl)";
				case "SR": return "Serbian (sr)";
				case "SV": return "Swedish (sv)";
				case "SE": return "Swedish (se)";
				case "TH": return "Thai (th)";
				case "TR": return "Turkish (tr)";
				case "UK": return "Ukrainian (uk)";
				case "UA": return "Ukrainian (ua)";
				case "VI": return "Vietnamese (vi)";
				case "ZH": return "Chinese";
				case "ZH-HANS": return "Chinese (zh-hans)";
				case "ZH-HANT": return "Chinese (zh-hant)";
				default: return language;

			}
		}

		public static List<NamingLanguage> PreferredNamingLanguages
		{
			get
			{
				List<NamingLanguage> lans = new List<NamingLanguage>();

				string[] slans = ServerSettings.LanguagePreference.Split(',');

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
