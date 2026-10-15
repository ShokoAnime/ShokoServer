using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

namespace Shoko.Tests.Extensions;

public class LanguageExtensionsTests
{
    [Theory]
    // Codes.
    [InlineData("ja", TitleLanguage.Japanese)]
    [InlineData("x-jat", TitleLanguage.Romaji)]
    [InlineData("pt-BR", TitleLanguage.BrazilianPortuguese)]
    [InlineData("fo", TitleLanguage.Faroese)]
    [InlineData("os", TitleLanguage.Ossetian)]
    [InlineData("ia", TitleLanguage.Interlingua)]
    [InlineData("hz", TitleLanguage.Herero)]
    [InlineData("ee", TitleLanguage.Ewe)]
    [InlineData("ie", TitleLanguage.Interlingue)]
    [InlineData("kr", TitleLanguage.Kanuri)]
    [InlineData("ng", TitleLanguage.Ndonga)]
    [InlineData("enm", TitleLanguage.MiddleEnglish)]
    [InlineData("cu", TitleLanguage.ChurchSlavic)]
    [InlineData("lg", TitleLanguage.Ganda)]
    [InlineData("cv", TitleLanguage.Chuvash)]
    [InlineData("an", TitleLanguage.Aragonese)]
    [InlineData("bo", TitleLanguage.Tibetan)]
    [InlineData("io", TitleLanguage.Ido)]
    [InlineData("kl", TitleLanguage.Kalaallisut)]
    [InlineData("li", TitleLanguage.Limburgish)]
    [InlineData("nd", TitleLanguage.NorthNdebele)]
    [InlineData("ss", TitleLanguage.Swati)]
    [InlineData("nds", TitleLanguage.LowGerman)]
    [InlineData("ak", TitleLanguage.Akan)]
    [InlineData("rm", TitleLanguage.Romansh)]
    [InlineData("to", TitleLanguage.Tongan)]
    [InlineData("new", TitleLanguage.Newari)]
    [InlineData("yap", TitleLanguage.Yapese)]
    [InlineData("pi", TitleLanguage.Pali)]
    [InlineData("pli", TitleLanguage.Pali)]
    [InlineData("aa", TitleLanguage.Afar)]
    [InlineData("ab", TitleLanguage.Abkhazian)]
    [InlineData("sa", TitleLanguage.Sanskrit)]
    [InlineData("av", TitleLanguage.Avaric)]
    [InlineData("sg", TitleLanguage.Sango)]
    [InlineData("nr", TitleLanguage.SouthNdebele)]
    [InlineData("nv", TitleLanguage.Navajo)]
    [InlineData("oj", TitleLanguage.Ojibwa)]
    [InlineData("vo", TitleLanguage.Volapuk)]
    [InlineData("ti", TitleLanguage.Tigrinya)]
    [InlineData("om", TitleLanguage.Oromo)]
    [InlineData("kv", TitleLanguage.Komi)]
    [InlineData("oc", TitleLanguage.Occitan)]
    [InlineData("su", TitleLanguage.Sundanese)]
    [InlineData("kj", TitleLanguage.Kuanyama)]
    [InlineData("wo", TitleLanguage.Wolof)]
    [InlineData("rw", TitleLanguage.Kinyarwanda)]
    [InlineData("ve", TitleLanguage.Venda)]
    [InlineData("gv", TitleLanguage.Manx)]
    [InlineData("bi", TitleLanguage.Bislama)]
    [InlineData("cr", TitleLanguage.Cree)]
    [InlineData("fy", TitleLanguage.Frisian)]
    [InlineData("ho", TitleLanguage.HiriMotu)]
    [InlineData("iu", TitleLanguage.Inuktitut)]
    [InlineData("mh", TitleLanguage.Marshallese)]
    [InlineData("za", TitleLanguage.Zhuang)]
    [InlineData("ks", TitleLanguage.Kashmiri)]
    [InlineData("ts", TitleLanguage.Tsonga)]
    [InlineData("dz", TitleLanguage.Dzongkha)]
    [InlineData("ii", TitleLanguage.SichuanYi)]
    [InlineData("frs", TitleLanguage.EastFrisian)]
    [InlineData("kaa", TitleLanguage.KaraKalpak)]
    [InlineData("rom", TitleLanguage.Romany)]
    [InlineData("ace", TitleLanguage.Acehnese)]
    [InlineData("aar", TitleLanguage.Afar)]
    [InlineData("abk", TitleLanguage.Abkhazian)]
    [InlineData("san", TitleLanguage.Sanskrit)]
    [InlineData("ava", TitleLanguage.Avaric)]
    [InlineData("sag", TitleLanguage.Sango)]
    [InlineData("nbl", TitleLanguage.SouthNdebele)]
    [InlineData("nav", TitleLanguage.Navajo)]
    [InlineData("oji", TitleLanguage.Ojibwa)]
    [InlineData("vol", TitleLanguage.Volapuk)]
    [InlineData("tir", TitleLanguage.Tigrinya)]
    [InlineData("orm", TitleLanguage.Oromo)]
    [InlineData("kom", TitleLanguage.Komi)]
    [InlineData("oci", TitleLanguage.Occitan)]
    [InlineData("sun", TitleLanguage.Sundanese)]
    [InlineData("kua", TitleLanguage.Kuanyama)]
    [InlineData("wol", TitleLanguage.Wolof)]
    [InlineData("kin", TitleLanguage.Kinyarwanda)]
    [InlineData("ven", TitleLanguage.Venda)]
    [InlineData("glv", TitleLanguage.Manx)]
    [InlineData("bis", TitleLanguage.Bislama)]
    [InlineData("cre", TitleLanguage.Cree)]
    [InlineData("fry", TitleLanguage.Frisian)]
    [InlineData("hmo", TitleLanguage.HiriMotu)]
    [InlineData("iku", TitleLanguage.Inuktitut)]
    [InlineData("mah", TitleLanguage.Marshallese)]
    [InlineData("zha", TitleLanguage.Zhuang)]
    [InlineData("kas", TitleLanguage.Kashmiri)]
    [InlineData("tso", TitleLanguage.Tsonga)]
    [InlineData("dzo", TitleLanguage.Dzongkha)]
    [InlineData("iii", TitleLanguage.SichuanYi)]
    [InlineData("mo", TitleLanguage.Romanian)]
    [InlineData("ace-Latn-ID", TitleLanguage.Acehnese)]
    [InlineData("Spanish (Catalan)", TitleLanguage.Catalan)]
    [InlineData("Spanish (Galician)", TitleLanguage.Galician)]
    [InlineData("Spanish (Basque)", TitleLanguage.Basque)]
    [InlineData("yue", TitleLanguage.Chinese)]
    [InlineData("jap", TitleLanguage.Japanese)]
    [InlineData("Deutsch", TitleLanguage.German)]
    [InlineData("English (English Audio)", TitleLanguage.English)]
    [InlineData("English (Japanese Audio)", TitleLanguage.English)]
    [InlineData("Japanese Audio (2 ch)", TitleLanguage.Japanese)]
    [InlineData("English Audio (2 ch)", TitleLanguage.English)]
    [InlineData("Japanese Audio (2-ch)", TitleLanguage.Japanese)]
    [InlineData("Japanese (Dub-Audio)", TitleLanguage.Japanese)]
    [InlineData("English Audio (5-1)", TitleLanguage.English)]
    [InlineData("Low German", TitleLanguage.LowGerman)]
    [InlineData("Japanese (2 ch)", TitleLanguage.Japanese)]
    [InlineData("JAPANESE AUDIO (2 CH)", TitleLanguage.Japanese)]
    [InlineData("Commentary Audio", TitleLanguage.Unknown)]
    [InlineData("Japanese (2-ch)", TitleLanguage.Japanese)]
    [InlineData("Japanese (5.1 ch)", TitleLanguage.Japanese)]
    [InlineData("Japanese (5.1)", TitleLanguage.Japanese)]
    [InlineData("No Subs", TitleLanguage.Unknown)]
    [InlineData("No Audio", TitleLanguage.Unknown)]
    [InlineData("No Subtitles", TitleLanguage.Unknown)]
    [InlineData("Main Audio", TitleLanguage.Unknown)]
    [InlineData("2 Audio", TitleLanguage.Unknown)]
    [InlineData("English, Japanese Audio", TitleLanguage.Unknown)]
    [InlineData("Audio", TitleLanguage.Unknown)]
    [InlineData("Chinese (Hakka)", TitleLanguage.Unknown)]
    [InlineData("Spanish (Mexico)", TitleLanguage.Unknown)]
    [InlineData("cmn-CN", TitleLanguage.Chinese)]
    // English names, as AniList reports them for its staff.
    [InlineData("Japanese", TitleLanguage.Japanese)]
    [InlineData("english", TitleLanguage.English)]
    [InlineData("Portuguese", TitleLanguage.Portuguese)]
    [InlineData("Brazilian Portuguese", TitleLanguage.BrazilianPortuguese)]
    [InlineData("Chinese", TitleLanguage.Chinese)]
    [InlineData("Mandarin", TitleLanguage.Chinese)]
    [InlineData("Tagalog", TitleLanguage.Filipino)]
    [InlineData("Malaysian", TitleLanguage.Malaysian)]
    // Unknown and empty.
    [InlineData("Klingon", TitleLanguage.Unknown)]
    [InlineData("", TitleLanguage.None)]
    public void GetTitleLanguage_ResolvesCodesAndNames(string language, TitleLanguage expected)
        => Assert.Equal(expected, language.GetTitleLanguage());

    // Malayalam's code "ml" still reads as Dutch, as it was stored that way. Obsolete values have no code of their own.
    public static IEnumerable<object[]> AllLanguages()
        => Enum.GetValues<TitleLanguage>()
            .Where(language => language is not TitleLanguage.Malayalam && !typeof(TitleLanguage).GetField(language.ToString())!.IsDefined(typeof(ObsoleteAttribute), false))
            .Select(language => new object[] { language });

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void EveryLanguage_RoundTripsThroughItsCode(TitleLanguage language)
        => Assert.Equal(language, language.GetString().GetTitleLanguage());

    [Theory]
    [InlineData(TitleLanguage.LowGerman, "nds", "Low German")]
    [InlineData(TitleLanguage.Akan, "ak", "Akan")]
    [InlineData(TitleLanguage.Romansh, "rm", "Romansh")]
    [InlineData(TitleLanguage.Tongan, "to", "Tongan")]
    [InlineData(TitleLanguage.Newari, "new", "Newari")]
    [InlineData(TitleLanguage.Yapese, "yap", "Yapese")]
    [InlineData(TitleLanguage.Pali, "pi", "Pali")]
    [InlineData(TitleLanguage.Afar, "aa", "Afar")]
    [InlineData(TitleLanguage.Abkhazian, "ab", "Abkhazian")]
    [InlineData(TitleLanguage.Sanskrit, "sa", "Sanskrit")]
    [InlineData(TitleLanguage.Avaric, "av", "Avaric")]
    [InlineData(TitleLanguage.Sango, "sg", "Sango")]
    [InlineData(TitleLanguage.SouthNdebele, "nr", "South Ndebele")]
    [InlineData(TitleLanguage.Navajo, "nv", "Navajo")]
    [InlineData(TitleLanguage.Ojibwa, "oj", "Ojibwa")]
    [InlineData(TitleLanguage.Volapuk, "vo", "Volapuk")]
    [InlineData(TitleLanguage.Tigrinya, "ti", "Tigrinya")]
    [InlineData(TitleLanguage.Oromo, "om", "Oromo")]
    [InlineData(TitleLanguage.Komi, "kv", "Komi")]
    [InlineData(TitleLanguage.Occitan, "oc", "Occitan")]
    [InlineData(TitleLanguage.Sundanese, "su", "Sundanese")]
    [InlineData(TitleLanguage.Kuanyama, "kj", "Kuanyama")]
    [InlineData(TitleLanguage.Wolof, "wo", "Wolof")]
    [InlineData(TitleLanguage.Kinyarwanda, "rw", "Kinyarwanda")]
    [InlineData(TitleLanguage.Venda, "ve", "Venda")]
    [InlineData(TitleLanguage.Manx, "gv", "Manx")]
    [InlineData(TitleLanguage.Bislama, "bi", "Bislama")]
    [InlineData(TitleLanguage.Cree, "cr", "Cree")]
    [InlineData(TitleLanguage.Frisian, "fy", "Frisian")]
    [InlineData(TitleLanguage.HiriMotu, "ho", "Hiri Motu")]
    [InlineData(TitleLanguage.Inuktitut, "iu", "Inuktitut")]
    [InlineData(TitleLanguage.Marshallese, "mh", "Marshallese")]
    [InlineData(TitleLanguage.Zhuang, "za", "Zhuang")]
    [InlineData(TitleLanguage.Kashmiri, "ks", "Kashmiri")]
    [InlineData(TitleLanguage.Tsonga, "ts", "Tsonga")]
    [InlineData(TitleLanguage.Dzongkha, "dz", "Dzongkha")]
    [InlineData(TitleLanguage.SichuanYi, "ii", "Sichuan Yi")]
    [InlineData(TitleLanguage.EastFrisian, "frs", "East Frisian")]
    [InlineData(TitleLanguage.KaraKalpak, "kaa", "Kara-Kalpak")]
    [InlineData(TitleLanguage.Romany, "rom", "Romany")]
    [InlineData(TitleLanguage.Acehnese, "ace", "Acehnese")]
    public void NewLanguages_RoundTripCodeAndDescription(TitleLanguage language, string code, string description)
    {
        Assert.Equal(code, language.GetString());
        Assert.Equal(language, code.GetTitleLanguage());
        Assert.Equal(description, language.GetDescription());
        Assert.Equal(language, description.GetTitleLanguage());
    }

    [Theory]
    [InlineData("English", true, TitleLanguage.English)]
    [InlineData("ja", true, TitleLanguage.Japanese)]
    [InlineData("ED", false, TitleLanguage.Unknown)]
    [InlineData("eps 2, 8", false, TitleLanguage.Unknown)]
    [InlineData("", false, TitleLanguage.None)]
    public void TryGetTitleLanguage_ProbesWithoutReporting(string text, bool expected, TitleLanguage expectedLanguage)
    {
        // Other classes parse unknown languages in parallel, into the same static event.
        var reported = new ConcurrentQueue<string>();
        void OnUnknown(string lang) => reported.Enqueue(lang);
        LanguageExtensions.OnUnknownLanguage += OnUnknown;
        try
        {
            Assert.Equal(expected, text.TryGetTitleLanguage(out var language));
            Assert.Equal(expectedLanguage, language);
            Assert.DoesNotContain(text, reported);
        }
        finally
        {
            LanguageExtensions.OnUnknownLanguage -= OnUnknown;
        }
    }

    [Theory]
    [InlineData("x-unk")]
    [InlineData("X-OTHER")]
    [InlineData("unk")]
    [InlineData("und")]
    [InlineData("zxx")]
    [InlineData("mul")]
    [InlineData("mis")]
    [InlineData("sh")]
    [InlineData("hbs")]
    [InlineData("map")]
    [InlineData("Undefined")]
    [InlineData("Undefined / Undefined")]
    [InlineData("unknown")]
    [InlineData("Keine Angabe")]
    [InlineData("instrumental")]
    public void GetTitleLanguage_KnownUnknownCodes_AreNotReported(string text)
    {
        // Other classes parse unknown languages in parallel, into the same static event.
        var reported = new ConcurrentQueue<string>();
        void OnUnknown(string lang) => reported.Enqueue(lang);
        LanguageExtensions.OnUnknownLanguage += OnUnknown;
        try
        {
            Assert.Equal(TitleLanguage.Unknown, text.GetTitleLanguage());
            Assert.DoesNotContain(text, reported);
        }
        finally
        {
            LanguageExtensions.OnUnknownLanguage -= OnUnknown;
        }
    }

    [Theory]
    [InlineData(TitleLanguage.Romaji, TitleLanguage.Japanese)]
    [InlineData(TitleLanguage.Pinyin, TitleLanguage.Chinese)]
    [InlineData(TitleLanguage.KoreanTranscription, TitleLanguage.Korean)]
    [InlineData(TitleLanguage.ThaiTranscription, TitleLanguage.Thai)]
    [InlineData(TitleLanguage.English, TitleLanguage.English)]
    public void GetSpokenLanguage_ResolvesTranscriptions(TitleLanguage language, TitleLanguage expected)
        => Assert.Equal(expected, language.GetSpokenLanguage());

    [Fact]
    public async Task GetTitleLanguage_ConcurrentUnknownStrings_DoesNotThrow()
    {
        var tasks = new List<Task<TitleLanguage>>();
        for (var i = 0; i < 100; i++)
        {
            var lang = $"unknown-{i % 10}";
            tasks.Add(Task.Run(() => lang.GetTitleLanguage()));
        }

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(TitleLanguage.Unknown, r));
    }

    [Theory]
    [InlineData("Tokyo, Japan", "JP", "ja", TitleLanguage.Japanese)]
    [InlineData("Osaka, Osaka Prefecture, Japan", "JP", "ja", TitleLanguage.Japanese)]
    [InlineData("Tokyo, Japan [日本]", "JP", "ja", TitleLanguage.Japanese)]
    [InlineData("Beijing, China", "CN", "zh", TitleLanguage.ChineseSimplified)]
    [InlineData("Shanghai, People's Republic of China", "CN", "zh", TitleLanguage.ChineseSimplified)]
    [InlineData("Taipei, Taiwan", "TW", "zh", TitleLanguage.ChineseTraditional)]
    [InlineData("Hong Kong", "HK", "zh", TitleLanguage.ChineseTraditional)]
    [InlineData("Seoul, South Korea", "KR", "ko", TitleLanguage.Korean)]
    [InlineData("Seoul, Republic of Korea", "KR", "ko", TitleLanguage.Korean)]
    [InlineData("Busan, Korea", "KR", "ko", TitleLanguage.Korean)]
    [InlineData("Bangkok, Thailand", "TH", "th", TitleLanguage.Thai)]
    [InlineData("Hanoi, Vietnam", "VN", "vi", TitleLanguage.Vietnamese)]
    [InlineData("Manila, Philippines", "PH", "en", TitleLanguage.English)]
    [InlineData("Jakarta, Indonesia", "ID", "id", TitleLanguage.Indonesian)]
    [InlineData("Kuala Lumpur, Malaysia", "MY", "ms", TitleLanguage.Malaysian)]
    [InlineData("Singapore", "SG", "en", TitleLanguage.English)]
    [InlineData("London, England, UK", "GB", "en", TitleLanguage.EnglishBritish)]
    [InlineData("Manchester, England", "GB", "en", TitleLanguage.EnglishBritish)]
    [InlineData("Los Angeles, California, USA", "US", "en", TitleLanguage.EnglishAmerican)]
    [InlineData("São Paulo, Brazil", "BR", "pt", TitleLanguage.BrazilianPortuguese)]
    [InlineData("Lisbon, Portugal", "PT", "pt", TitleLanguage.Portuguese)]
    [InlineData("Montreal, Quebec, Canada", "CA", "en", TitleLanguage.EnglishCanadian)]
    [InlineData("Somewhere, Atlantis", null, null, null)]
    public void APlaceNamesItsCountryAndTheCountryItsLanguage(string place, string? country, string? languageCode, TitleLanguage? language)
    {
        var found = LanguageExtensions.TryGetCountryCodeFromPlace(place, out var countryCode);
        var code = found ? LanguageExtensions.GetLanguageCodeFromCountryCode(countryCode) : null;

        Assert.Equal(country, found ? countryCode : null);
        Assert.Equal(languageCode, code);
        Assert.Equal(language, LanguageExtensions.GetOriginalLanguage(code, found ? countryCode : null));
    }

    [Theory]
    [InlineData("fr", "CA", TitleLanguage.FrenchCanadian)]
    [InlineData("fr", "FR", TitleLanguage.French)]
    [InlineData("zh", "MO", TitleLanguage.ChineseTraditional)]
    [InlineData("zh", "SG", TitleLanguage.ChineseSimplified)]
    [InlineData("ja", null, TitleLanguage.Japanese)]
    [InlineData(null, "JP", null)]
    public void TheOriginalLanguageIsToldApartByTheCountry(string? languageCode, string? countryCode, TitleLanguage? expected)
        => Assert.Equal(expected, LanguageExtensions.GetOriginalLanguage(languageCode, countryCode));
}
