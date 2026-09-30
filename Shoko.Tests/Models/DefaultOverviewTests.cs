using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the default overview of a Shoko series and episode: their AniDB entry's description,
/// kept once worked out and worked out again once the AniDB entry is refreshed, as the series'
/// default title is.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class DefaultOverviewTests
{
    private static RepoFactoryScope ScopeWith(AniDB_Anime[] anime, AniDB_Episode[] episodes, out MetadataTextManager manager)
    {
        manager = TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter()));
        return new RepoFactoryScope()
            .Set(manager)
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID, anime)
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, episodes);
    }

    [Fact]
    public void TheSeriesOverviewIsKeptUntilTheAnimeIsRefreshed()
    {
        var anime = new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = 10, Description = "Before." };
        using var scope = ScopeWith([anime], [], out var manager);
        var series = (IWithOverviews)new AnimeSeries { AnimeSeriesID = 5, AniDB_ID = 10 };

        var first = series.DefaultOverview;
        Assert.Equal("Before.", first?.Value);
        Assert.Equal(MetadataSource.AniDB, first?.Source);
        Assert.Equal(TitleLanguage.English, first?.Language);
        Assert.Same(first, series.DefaultOverview);

        anime.Description = "After.";
        Assert.Same(first, series.DefaultOverview);

        manager.Invalidate(((IMetadata)anime).ID);
        Assert.Equal("After.", series.DefaultOverview?.Value);
    }

    [Fact]
    public void TheSeriesHasNoOverviewWhenTheAnimeHasNoDescription()
    {
        var anime = new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = 10, Description = string.Empty };
        using var scope = ScopeWith([anime], [], out var manager);
        var series = (IWithOverviews)new AnimeSeries { AnimeSeriesID = 5, AniDB_ID = 10 };

        Assert.Null(series.DefaultOverview);

        anime.Description = "Written since.";
        manager.Invalidate(((IMetadata)anime).ID);
        Assert.Equal("Written since.", series.DefaultOverview?.Value);
    }

    [Fact]
    public void TheEpisodeOverviewIsKeptUntilTheAnidbEpisodeIsRefreshed()
    {
        var anidbEpisode = new AniDB_Episode { AniDB_EpisodeID = 100, EpisodeID = 100, AnimeID = 10, EpisodeNumber = 1, Description = "Before." };
        using var scope = ScopeWith([], [anidbEpisode], out var manager);
        var episode = (IWithOverviews)new AnimeEpisode { AnimeEpisodeID = 7, AniDB_EpisodeID = 100 };

        var first = episode.DefaultOverview;
        Assert.Equal("Before.", first?.Value);
        Assert.Equal(MetadataSource.AniDB, first?.Source);
        Assert.Same(first, episode.DefaultOverview);

        anidbEpisode.Description = "After.";
        Assert.Same(first, episode.DefaultOverview);

        manager.Invalidate(((IMetadata)anidbEpisode).ID);
        Assert.Equal("After.", episode.DefaultOverview?.Value);
    }

    [Fact]
    public void TheEpisodeHasNoOverviewWithoutItsAnidbEpisode()
    {
        using var scope = ScopeWith([], [], out _);
        var episode = (IWithOverviews)new AnimeEpisode { AnimeEpisodeID = 7, AniDB_EpisodeID = 100 };

        Assert.Null(episode.DefaultOverview);
    }
}
