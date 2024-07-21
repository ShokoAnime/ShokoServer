using System.Xml.Serialization;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AnimeTitle
    {
        public DataSourceEnum Source { get; set; }

        public TitleLanguage Language { get; set; }

        public string LanguageCode { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public TitleType Type { get; set; }
    }

    public enum TitleLanguage
    {
        Unknown = 0,
        English = 1,
        Romaji,
        Japanese,
        Afrikaans,
        Arabic,
        Bangladeshi,
        Bulgarian,
        FrenchCanadian,
        Czech,
        Danish,
        German,
        Greek,
        Spanish,
        Estonian,
        Finnish,
        French,
        Galician,
        Hebrew,
        Hungarian,
        Italian,
        Korean,
        Lithuanian,
        Mongolian,
        Malaysian,
        Dutch,
        Norwegian,
        Polish,
        Portuguese,
        BrazilianPortuguese,
        Romanian,
        Russian,
        Slovak,
        Slovenian,
        Serbian,
        Swedish,
        Thai,
        Turkish,
        Ukrainian,
        Vietnamese,
        Chinese,
        ChineseSimplified,
        ChineseTraditional,
        Pinyin,
        Latin,
        Albanian,
        Basque,
        Bengali,
        Bosnian,
        Amharic,
        Armenian,
        Azerbaijani,
        Belarusian,
        Catalan,
        Chichewa,
        Corsican,
        Croatian,
        Divehi,
        Esperanto,
        Fijian,
        Georgian,
        Gujarati,
        HaitianCreole,
        Hausa,
        Icelandic,
        Igbo,
        Indonesian,
        Irish,
        Javanese,
        Kannada,
        Kazakh,
        Khmer,
        Kurdish,
        Kyrgyz,
        Lao,
        Latvian,
        Luxembourgish,
        Macedonian,
        Malagasy,
        Malayalam,
        Maltese,
        Maori,
        Marathi,
        MyanmarBurmese,
        Nepali,
        Oriya,
        Pashto,
        Persian,
        Punjabi,
        Quechua,
        Samoan,
        ScotsGaelic,
        Sesotho,
        Shona,
        Sindhi,
        Sinhala,
        Somali,
        Swahili,
        Tajik,
        Tamil,
        Tatar,
        Telugu,
        Turkmen,
        Uighur,
        Uzbek,
        Welsh,
        Xhosa,
        Yiddish,
        Yoruba,
        Zulu,
    }

    public enum TitleType
    {
        [XmlEnum("none")]
        None = 0,
        [XmlEnum("main")]
        Main = 1,
        [XmlEnum("official")]
        Official = 2,
        [XmlEnum("short")]
        Short = 3,
        [XmlEnum("syn")]
        Synonym = 4,
        [XmlEnum("card")]
        TitleCard = 5,
        [XmlEnum("kana")]
        KanjiReading = 6,
    }
}
