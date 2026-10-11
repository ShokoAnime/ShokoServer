using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.MediaInfo;
using Xunit;

namespace Shoko.Tests.MediaInfo;

public class MediaInfoLanguageTableTests
{
    public static IEnumerable<object[]> SharedLanguages()
        => Enum.GetValues<TitleLanguage>()
            .Where(language => language.GetString() is { Length: 2 or 3 } code && code != "unk" && MediaInfoUtility.GetLanguageMapping(code) is not null)
            .Select(language => new object[] { language });

    // The table keeps the ISO 639 name where the description is a UI label ("English (Any)") or a different name.
    private static readonly Dictionary<TitleLanguage, string> _isoNames = new()
    {
        [TitleLanguage.English] = "English",
        [TitleLanguage.Japanese] = "Japanese",
        [TitleLanguage.Chinese] = "Chinese",
        [TitleLanguage.Malaysian] = "Malay",
        [TitleLanguage.Sinhala] = "Sinhalese",
        [TitleLanguage.Punjabi] = "Panjabi",
        [TitleLanguage.Pashto] = "Pushto",
        [TitleLanguage.Kyrgyz] = "Kirghiz",
        [TitleLanguage.Sesotho] = "Sotho",
        [TitleLanguage.MyanmarBurmese] = "Burmese",
        [TitleLanguage.HaitianCreole] = "Haitian",
        [TitleLanguage.ScotsGaelic] = "Gaelic",
        [TitleLanguage.Filipino] = "Tagalog",
    };

    [Theory]
    [MemberData(nameof(SharedLanguages))]
    public void GetLanguageMapping_NameMatchesTitleLanguage(TitleLanguage language)
        => Assert.Equal(_isoNames.GetValueOrDefault(language) ?? language.GetDescription(), MediaInfoUtility.GetLanguageMapping(language.GetString())!.Item2);

    [Theory]
    [InlineData(TitleLanguage.LowGerman)]
    [InlineData(TitleLanguage.Akan)]
    [InlineData(TitleLanguage.Romansh)]
    [InlineData(TitleLanguage.Tongan)]
    [InlineData(TitleLanguage.Newari)]
    [InlineData(TitleLanguage.Yapese)]
    [InlineData(TitleLanguage.Pali)]
    public void GetLanguageMapping_HasNewLanguages(TitleLanguage language)
        => Assert.NotNull(MediaInfoUtility.GetLanguageMapping(language.GetString()));

    [Theory]
    [InlineData("romansh", "rm")]
    [InlineData("tongan", "to")]
    [InlineData("raetoromance", null)]
    [InlineData("tonga", null)]
    public void GetLanguageFromName_UsesTheAlignedNames(string name, string? code)
        => Assert.Equal(code, MediaInfoUtility.GetLanguageFromName(name));
}
