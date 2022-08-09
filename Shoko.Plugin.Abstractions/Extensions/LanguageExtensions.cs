using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Extensions;

public static class LanguageExtensions
{
    public static TitleLanguage GetTitleLanguage(this string lang)
    {
        return lang.ToUpper() switch
        {
            "EN" => TitleLanguage.English,
            "X-JAT" => TitleLanguage.Romaji,
            "JA" => TitleLanguage.Japanese,
            "AR" => TitleLanguage.Arabic,
            "BD" => TitleLanguage.Bangladeshi,
            "BG" => TitleLanguage.Bulgarian,
            "CA" => TitleLanguage.FrenchCanadian,
            "CS" => TitleLanguage.Czech,
            "CZ" => TitleLanguage.Czech,
            "DA" => TitleLanguage.Danish,
            "DK" => TitleLanguage.Danish,
            "DE" => TitleLanguage.German,
            "EL" => TitleLanguage.Greek,
            "ES" => TitleLanguage.Spanish,
            "ET" => TitleLanguage.Estonian,
            "FI" => TitleLanguage.Finnish,
            "FR" => TitleLanguage.French,
            "GL" => TitleLanguage.Galician,
            "GR" => TitleLanguage.Greek,
            "HE" => TitleLanguage.Hebrew,
            "HU" => TitleLanguage.Hungarian,
            "IL" => TitleLanguage.Hebrew,
            "IT" => TitleLanguage.Italian,
            "KO" => TitleLanguage.Korean,
            "LT" => TitleLanguage.Lithuania,
            "MN" => TitleLanguage.Mongolian,
            "MS" => TitleLanguage.Malaysian,
            "MY" => TitleLanguage.Malaysian,
            "NL" => TitleLanguage.Dutch,
            "NO" => TitleLanguage.Norwegian,
            "PL" => TitleLanguage.Polish,
            "PT" => TitleLanguage.Portuguese,
            "PT-BR" => TitleLanguage.BrazilianPortuguese,
            "RO" => TitleLanguage.Romanian,
            "RU" => TitleLanguage.Russian,
            "SK" => TitleLanguage.Slovak,
            "SL" => TitleLanguage.Slovenian,
            "SR" => TitleLanguage.Serbian,
            "SV" => TitleLanguage.Swedish,
            "SE" => TitleLanguage.Swedish, // Common country vs language code mixup
            "TH" => TitleLanguage.Thai,
            "TR" => TitleLanguage.Turkish,
            "UK" => TitleLanguage.Ukrainian, // Modern ISO code
            "UA" => TitleLanguage.Ukrainian, // Deprecated ISO code
            "VI" => TitleLanguage.Vietnamese,
            "ZH" => TitleLanguage.Chinese,
            "X-ZHT" => TitleLanguage.Pinyin,
            "ZH-HANS" => TitleLanguage.ChineseSimplified,
            "ZH-HANT" => TitleLanguage.ChineseTraditional,
            _ => TitleLanguage.Unknown,
        };
    }
    
    public static string GetDescription(this TitleLanguage lang)
    {
        return lang switch
        {
            TitleLanguage.Romaji => "Japanese (romanji/x-jat)",
            TitleLanguage.Japanese => "Japanese (kanji)",
            TitleLanguage.Bangladeshi => "Bangladesh",
            TitleLanguage.FrenchCanadian => "Canadian-French",
            TitleLanguage.BrazilianPortuguese => "Brazilian Portuguese",
            TitleLanguage.Chinese => "Chinese (any)",
            TitleLanguage.ChineseSimplified => "Chinese (simplified)",
            TitleLanguage.ChineseTraditional => "Chinese (traditional)",
            TitleLanguage.Pinyin => "Chinese (pinyin/x-zhn)",
            _ => lang.ToString(),
        };
    }

    public static string GetString(this TitleLanguage lang)
    {
        return lang switch
        {
            TitleLanguage.English => "en",
            TitleLanguage.Romaji => "x-jat",
            TitleLanguage.Japanese => "ja",
            TitleLanguage.Arabic => "ar",
            TitleLanguage.Bangladeshi => "bd",
            TitleLanguage.Bulgarian => "bg",
            TitleLanguage.FrenchCanadian => "ca",
            TitleLanguage.Czech => "cz",
            TitleLanguage.Danish => "da",
            TitleLanguage.German => "de",
            TitleLanguage.Greek => "gr",
            TitleLanguage.Spanish => "es",
            TitleLanguage.Estonian => "et",
            TitleLanguage.Finnish => "fi",
            TitleLanguage.French => "fr",
            TitleLanguage.Galician => "gl",
            TitleLanguage.Hebrew => "he",
            TitleLanguage.Hungarian => "hu",
            TitleLanguage.Italian => "it",
            TitleLanguage.Korean => "ko",
            TitleLanguage.Lithuania => "lt",
            TitleLanguage.Mongolian => "mn",
            TitleLanguage.Malaysian => "ms",
            TitleLanguage.Dutch => "ml",
            TitleLanguage.Norwegian => "no",
            TitleLanguage.Polish => "pl",
            TitleLanguage.Portuguese => "pt",
            TitleLanguage.BrazilianPortuguese => "pt-br",
            TitleLanguage.Romanian => "ro",
            TitleLanguage.Russian => "ru",
            TitleLanguage.Slovak => "sk",
            TitleLanguage.Slovenian => "sl",
            TitleLanguage.Serbian => "sr",
            TitleLanguage.Swedish => "sv",
            TitleLanguage.Thai => "th",
            TitleLanguage.Turkish => "tr",
            TitleLanguage.Ukrainian => "uk",
            TitleLanguage.Vietnamese => "vi",
            TitleLanguage.Chinese => "zh",
            TitleLanguage.Pinyin => "x-zht",
            TitleLanguage.ChineseSimplified => "zh-hans",
            TitleLanguage.ChineseTraditional => "zh-hant",
            _ => "unk",
        };
    }
    
    public static TitleType GetTitleType(this string type)
    {
        return type.ToLowerInvariant() switch
        {
            "main" => TitleType.Main,
            "official" => TitleType.Official,
            "synonym" => TitleType.Synonym,
            "syn" => TitleType.Synonym,
            "short" => TitleType.Short,
            "card" => TitleType.Card,
            "kana" => TitleType.Katakana,
            _ => TitleType.None,
        };
    }
}
