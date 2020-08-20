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
        Latin,
        Albanian,
        Basque,
        Bengali,
        Bosnian
    }

    public enum TitleType
    {
        None = 0,
        Main = 1,
        Official = 2,
        Short = 3,
        Synonym = 4
    }
}