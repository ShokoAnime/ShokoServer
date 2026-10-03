using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers what the cross-reference store refuses to write: a series or film
/// link to nothing. An episode may still be linked to nothing.
/// </summary>
public class MetadataLinkRefusalTests
{
    #region Helpers

    private const int AnimeID = 100;

    private static CancellationToken Token
        => TestContext.Current.CancellationToken;

    private static MetadataGuid Tmdb(MetadataEntityType entityType, string id)
        => new(MetadataSource.TMDB, entityType, id);

    private static MetadataSeriesLinkData SeriesLink(MetadataSource source, MetadataGuid? providerID)
        => new() { Source = source, AnidbAnimeID = AnimeID, ProviderID = providerID };

    private static MetadataMovieLinkData MovieLink(MetadataSource source, MetadataGuid? providerID)
        => new() { Source = source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1001, ProviderID = providerID };

    private static MetadataEpisodeLinkData EpisodeLink(
        MetadataSource source,
        MetadataGuid? providerID,
        MetadataGuid? parentID = null,
        MetadataGuid? seasonID = null
    )
        => new()
        {
            Source = source,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 1001,
            ProviderID = providerID,
            ProviderParentID = parentID,
            SeasonID = seasonID,
        };

    #endregion

    #region Links to nothing

    [Fact]
    public async Task AFilmLinkToNothingIsRefused()
    {
        var links = new WritableLinkStore();

        await Assert.ThrowsAsync<ArgumentException>(() => links.Store.MergeMovieLinks([MovieLink(TestSources.Plugin, null)], cancellationToken: Token));

        Assert.Empty(links.Movies.GetAll());
    }

    [Fact]
    public async Task AnEpisodeLinkToNothingIsStillKept()
    {
        var links = new WritableLinkStore();

        await links.Store.MergeEpisodeLinks([EpisodeLink(MetadataSource.TMDB, null)], cancellationToken: Token);

        var stored = Assert.Single(links.Episodes.GetAll());
        Assert.True(stored.IsUnlinked);
    }

    [Fact]
    public async Task OneRefusedLinkLeavesTheWholeWriteUndone()
    {
        var links = new WritableLinkStore();

        await Assert.ThrowsAsync<ArgumentException>(() => links.Store.MergeSeriesLinks([
            SeriesLink(TestSources.Plugin, new(TestSources.Plugin, MetadataEntityType.Series, "21")),
            SeriesLink(TestSources.Plugin, null),
        ], cancellationToken: Token));

        Assert.Empty(links.Series.GetAll());
    }

    [Fact]
    public void TheServersOwnWritesRefuseALinkToNothingToo()
    {
        var links = new WritableLinkStore();

        Assert.Throws<ArgumentException>(() => links.Store.AddSeriesLink(MetadataSource.TMDB, AnimeID, null, MatchRating.UserVerified));
        Assert.Throws<ArgumentException>(() => links.Store.AddMovieLink(MetadataSource.TMDB, AnimeID, 1001, null, MatchRating.UserVerified));

        Assert.Empty(links.Series.GetAll());
        Assert.Empty(links.Movies.GetAll());
    }

    #endregion

    #region Episode groups

    [Fact]
    public async Task ATmdbEpisodeInAnEpisodeGroupIsStored()
    {
        var links = new WritableLinkStore();

        // An alternate ordering's season is an episode group, named by text.
        var episodeGroup = Tmdb(MetadataEntityType.Season, "5acf93e60e0a26346d0000ce");
        await links.Store.MergeEpisodeLinks(
            [EpisodeLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Episode, "44"), Tmdb(MetadataEntityType.Series, "42"), episodeGroup)],
            cancellationToken: Token
        );

        Assert.Equal("44", Assert.Single(links.Episodes.GetAll()).ProviderID);
    }

    #endregion
}
