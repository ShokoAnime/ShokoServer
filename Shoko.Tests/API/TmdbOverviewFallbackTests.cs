using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.API.v3.Helpers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// An APIv3 TMDB entry with no overview in a preferred language falls back to
/// TMDB's English one only, as it did while TMDB was a core source.
/// </summary>
public class TmdbOverviewFallbackTests
{
    private static IWithOverviews Entry(params IText[] overviews)
    {
        var entry = new Mock<IWithOverviews>();
        entry.SetupGet(e => e.Overviews).Returns(overviews);
        entry.SetupGet(e => e.PreferredOverview).Returns((IText?)null);
        entry.SetupGet(e => e.DefaultOverview).Returns(overviews.Length > 0 ? overviews[0] : null);
        return entry.Object;
    }

    private static TextStub Overview(string languageCode, string countryCode, string value)
        => new() { Source = MetadataSource.TMDB, Language = TitleLanguage.Unknown, LanguageCode = languageCode, CountryCode = countryCode, Value = value };

    [Fact]
    public void AnEntryWithoutAnEnglishOverviewShowsNone()
    {
        var entry = Entry(Overview("zh", "CN", "中文简介"));

        Assert.Equal(string.Empty, TmdbCompatibility.PreferredOverview(entry).Value);
        Assert.Equal(string.Empty, TmdbCompatibility.DefaultOverview(entry));
    }

    [Fact]
    public void AnEntryFallsBackToItsEnglishOverview()
    {
        var entry = Entry(Overview("zh", "CN", "中文简介"), Overview("en", "US", "In English."));

        Assert.Equal("In English.", TmdbCompatibility.PreferredOverview(entry).Value);
        Assert.Equal("In English.", TmdbCompatibility.DefaultOverview(entry));
    }
}
