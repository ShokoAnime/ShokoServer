using System.Collections.Generic;
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

    [Theory]
    [InlineData("English", true, TitleLanguage.English)]
    [InlineData("ja", true, TitleLanguage.Japanese)]
    [InlineData("ED", false, TitleLanguage.Unknown)]
    [InlineData("eps 2, 8", false, TitleLanguage.Unknown)]
    [InlineData("", false, TitleLanguage.None)]
    public void TryGetTitleLanguage_ProbesWithoutReporting(string text, bool expected, TitleLanguage expectedLanguage)
    {
        var reported = new List<string>();
        void OnUnknown(string lang) => reported.Add(lang);
        LanguageExtensions.OnUnknownLanguage += OnUnknown;
        try
        {
            Assert.Equal(expected, text.TryGetTitleLanguage(out var language));
            Assert.Equal(expectedLanguage, language);
            Assert.Empty(reported);
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
}
