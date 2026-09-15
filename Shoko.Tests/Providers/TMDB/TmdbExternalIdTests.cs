using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.General;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// User Story 2: external-ID cross-links stay correct after the TMDbLib upgrade.
/// <c>ExternalIdsTvShow/TvEpisode.TvdbId</c> changed from <c>string?</c> to <c>int?</c>
/// (jellyfin/TMDbLib#626); these pin the <c>Update*ExternalIDs</c> handlers to the
/// post-upgrade type and the "clear when absent / non-positive" contract.
/// </summary>
public class TmdbExternalIdTests
{
    [Theory]
    [InlineData(259140, 259140)]
    [InlineData(1, 1)]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    [InlineData(null, null)]
    public void UpdateShowExternalIDs_StoresPositiveTvdbIdElseNull(int? wireTvdbId, int? expected)
    {
        var show = new TMDB_Show();
        var changed = TmdbMetadataService.UpdateShowExternalIDs(show, new ExternalIdsTvShow { TvdbId = wireTvdbId });

        Assert.Equal(expected, show.TvdbShowID);
        Assert.Equal(expected is not null, changed);
    }

    [Theory]
    [InlineData(4298471, 4298471)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    public void UpdateEpisodeExternalIDs_StoresPositiveTvdbIdElseNull(int? wireTvdbId, int? expected)
    {
        var episode = new TMDB_Episode();
        var changed = TmdbMetadataService.UpdateEpisodeExternalIDs(episode, new ExternalIdsTvEpisode { TvdbId = wireTvdbId });

        Assert.Equal(expected, episode.TvdbEpisodeID);
        Assert.Equal(expected is not null, changed);
    }

    [Fact]
    public void UpdateShowExternalIDs_PreviouslySetThenRemoved_IsCleared()
    {
        var show = new TMDB_Show { TvdbShowID = 259140 };

        Assert.True(TmdbMetadataService.UpdateShowExternalIDs(show, new ExternalIdsTvShow { TvdbId = null }));
        Assert.Null(show.TvdbShowID);
    }

    [Fact]
    public void UpdateShowExternalIDs_UnchangedValue_ReturnsFalse()
    {
        var show = new TMDB_Show { TvdbShowID = 259140 };

        Assert.False(TmdbMetadataService.UpdateShowExternalIDs(show, new ExternalIdsTvShow { TvdbId = 259140 }));
        Assert.Equal(259140, show.TvdbShowID);
    }

    [Fact]
    public void UpdateShowExternalIDs_NullContainer_ClearsWithoutThrowing()
    {
        var show = new TMDB_Show { TvdbShowID = 259140 };

        Assert.True(TmdbMetadataService.UpdateShowExternalIDs(show, null));
        Assert.Null(show.TvdbShowID);
    }

    [Theory]
    [InlineData("tt0094625")]
    [InlineData(null)]
    public void UpdateMovieExternalIDs_ImdbId_StoredVerbatim(string? imdbId)
    {
        var movie = new TMDB_Movie();
        var changed = TmdbMetadataService.UpdateMovieExternalIDs(movie, new ExternalIdsMovie { ImdbId = imdbId });

        Assert.Equal(imdbId, movie.ImdbMovieID);
        Assert.Equal(imdbId is not null, changed);
    }

    [Fact]
    public void UpdateMovieExternalIDs_NullContainer_ClearsWithoutThrowing()
    {
        var movie = new TMDB_Movie { ImdbMovieID = "tt0094625" };

        Assert.True(TmdbMetadataService.UpdateMovieExternalIDs(movie, null));
        Assert.Null(movie.ImdbMovieID);
    }
}
