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
}
