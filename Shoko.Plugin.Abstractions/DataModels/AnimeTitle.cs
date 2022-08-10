using System.Xml.Serialization;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AnimeTitle
    {
        public TitleLanguage Language { get; set; }
        public string LanguageCode { get; set; }
        public string Title { get; set; }
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
        Lithuania,
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
        Bosnian
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