using System;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Providers.Anilist;
using Xunit;

namespace Shoko.Tests.Providers.Anilist;

public class AnilistUtilityTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(21, 1150)]
    [InlineData(200_000, 4095)]
    [InlineData(AnilistUtility.MaxAnimeID, AnilistUtility.MaxEpisodeNumber)]
    public void PackEpisodeID_RoundTrips_AndStaysPositive(int animeId, int episodeNumber)
    {
        var packed = AnilistUtility.PackEpisodeID(animeId, episodeNumber);

        Assert.True(packed > 0, $"Expected a positive id, got {packed}");
        Assert.Equal((animeId, episodeNumber), AnilistUtility.UnpackEpisodeID(packed));
    }

    [Fact]
    public void PackEpisodeID_LargestPair_IsIntMaxValue()
    {
        Assert.Equal(int.MaxValue, AnilistUtility.PackEpisodeID(AnilistUtility.MaxAnimeID, AnilistUtility.MaxEpisodeNumber));
    }

    [Fact]
    public void PackEpisodeID_OrdersByAnimeThenEpisode()
    {
        Assert.True(AnilistUtility.PackEpisodeID(21, 2) > AnilistUtility.PackEpisodeID(21, 1));
        Assert.True(AnilistUtility.PackEpisodeID(22, 1) > AnilistUtility.PackEpisodeID(21, AnilistUtility.MaxEpisodeNumber));
    }

    [Theory]
    [InlineData(AnilistUtility.MaxAnimeID + 1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, AnilistUtility.MaxEpisodeNumber + 1)]
    [InlineData(1, -1)]
    public void PackEpisodeID_Throws_OutsideRange(int animeId, int episodeNumber)
    {
        Assert.False(AnilistUtility.CanPackEpisodeID(animeId, episodeNumber));
        Assert.Throws<ArgumentOutOfRangeException>(() => AnilistUtility.PackEpisodeID(animeId, episodeNumber));
    }

    [Theory]
    [InlineData("TV", AnimeType.TVSeries)]
    [InlineData("tv_short", AnimeType.TVShort)]
    [InlineData("MOVIE", AnimeType.Movie)]
    [InlineData("ONA", AnimeType.Web)]
    [InlineData("SOMETHING_NEW", AnimeType.Unknown)]
    [InlineData(null, AnimeType.Unknown)]
    public void ParseFormat_MapsKnownValues_AndFallsBackToUnknown(string? format, AnimeType expected)
        => Assert.Equal(expected, AnilistUtility.ParseFormat(format));

    [Theory]
    [InlineData("RELEASING", AnilistMediaStatus.Releasing)]
    [InlineData("not_yet_released", AnilistMediaStatus.NotYetReleased)]
    [InlineData("SOMETHING_NEW", AnilistMediaStatus.Unknown)]
    [InlineData(null, AnilistMediaStatus.Unknown)]
    public void ParseMediaStatus_MapsKnownValues_AndFallsBackToUnknown(string? status, AnilistMediaStatus expected)
        => Assert.Equal(expected, AnilistUtility.ParseMediaStatus(status));

    [Theory]
    [InlineData("LIGHT_NOVEL", AnilistMediaSource.LightNovel)]
    [InlineData("SOMETHING_NEW", AnilistMediaSource.Other)]
    [InlineData(null, AnilistMediaSource.Unknown)]
    public void ParseMediaSource_MapsKnownValues_UnknownStringsBecomeOther(string? source, AnilistMediaSource expected)
        => Assert.Equal(expected, AnilistUtility.ParseMediaSource(source));

    [Theory]
    [InlineData("FALL", YearlySeason.Fall)]
    [InlineData("winter", YearlySeason.Winter)]
    [InlineData(null, null)]
    [InlineData("MONSOON", null)]
    public void ParseSeason_RoundTripsWithToSeason(string? season, YearlySeason? expected)
    {
        Assert.Equal(expected, AnilistUtility.ParseSeason(season));
        if (expected is { } value)
            Assert.Equal(season!.ToUpperInvariant(), AnilistUtility.ToSeason(value));
    }

    [Theory]
    [InlineData("ADR Director (English)", "ADR Director", TitleLanguage.English)]
    [InlineData("ADR Director (Brazilian Portuguese)", "ADR Director", TitleLanguage.BrazilianPortuguese)]
    [InlineData("ADR Script (Spanish)", "ADR Script", TitleLanguage.Spanish)]
    [InlineData("Theme Song Performance (ED)", "Theme Song Performance (ED)", null)]
    [InlineData("2nd Key Animation (eps 2, 8)", "2nd Key Animation (eps 2, 8)", null)]
    [InlineData("Director", "Director", null)]
    [InlineData("", "", null)]
    [InlineData(null, "", null)]
    public void SplitRoleLanguage_StripsOnlyLanguageQualifiers(string? role, string expectedRole, TitleLanguage? expectedLanguage)
        => Assert.Equal((expectedRole, expectedLanguage), AnilistUtility.SplitRoleLanguage(role));

    [Theory]
    [InlineData("ja", TitleLanguage.Romaji)]
    [InlineData("JA", TitleLanguage.Romaji)]
    [InlineData("zh", TitleLanguage.Pinyin)]
    [InlineData("zh-TW", TitleLanguage.Pinyin)]
    [InlineData("ko", TitleLanguage.KoreanTranscription)]
    [InlineData("th", TitleLanguage.ThaiTranscription)]
    [InlineData("", TitleLanguage.Romaji)]
    [InlineData(null, TitleLanguage.Romaji)]
    public void GetMainTitleLanguage_FollowsTheOriginalLanguage(string? originalLanguageCode, TitleLanguage expected)
        => Assert.Equal(expected, AnilistUtility.GetMainTitleLanguage(originalLanguageCode));

    [Fact]
    public void StripHtml_ConvertsBreaks_DropsTags_DecodesEntities_AndCollapsesBlankLines()
    {
        const string html = "First line.<br>Second &amp; <i>third</i>.<br><br><br>\n\n\nAfter gap.";

        var text = AnilistUtility.StripHtml(html);

        Assert.Equal("First line.\nSecond & third.\n\nAfter gap.", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StripHtml_EmptyInput_ReturnsEmpty(string? html)
        => Assert.Equal(string.Empty, AnilistUtility.StripHtml(html));
}
