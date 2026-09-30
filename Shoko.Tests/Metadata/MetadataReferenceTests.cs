using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers how entries name the entries they point at: every reference is the
/// other entry's <see cref="MetadataGuid"/>, built from the columns already
/// stored.
/// </summary>
public class MetadataReferenceTests
{
    #region Parents

    [Fact]
    public void AnAnidbEpisodeNamesItsAnimeAndItsSeason()
    {
        var anime = new AniDB_Anime { AnimeID = 1 };
        IEpisode episode = new AniDB_Episode { EpisodeID = 2, AnimeID = 1, EpisodeType = EpisodeType.Special };
        IMetadata season = new AniDB_Season(anime, EpisodeType.Special, 0);

        Assert.Equal(((IMetadata)anime).ID, episode.SeriesID);
        Assert.Equal(season.ID, episode.SeasonID);
        Assert.Equal(((IMetadata)anime).ID, ((ISeason)season).SeriesID);
    }

    [Fact]
    public void AnAnidbEpisodeOutsideTheSeasonsNamesNoSeason()
    {
        IEpisode episode = new AniDB_Episode { EpisodeID = 2, AnimeID = 1, EpisodeType = EpisodeType.Credits };

        Assert.Null(episode.SeasonID);
    }

    [Fact]
    public void AShokoSeasonNamesItsSeries()
    {
        var series = new AnimeSeries { AnimeSeriesID = 12 };
        ISeason season = new AnimeSeason(series, EpisodeType.Episode, 1);

        Assert.Equal(new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, "12"), season.SeriesID);
    }

    #endregion

    #region Relations and Suggestions

    [Fact]
    public void AReversedRelationSwapsItsEnds()
    {
        IRelatedMetadata relation = new AniDB_Anime_Relation { AnimeID = 1, RelatedAnimeID = 2, RelationType = "sequel" };

        Assert.Equal(relation.BaseID, relation.Reversed.RelatedID);
        Assert.Equal(relation.RelatedID, relation.Reversed.BaseID);
    }

    [Fact]
    public void ATmdbSuggestionNamesBothEndsAsTheKindTheyAre()
    {
        ISuggestedMetadata suggestion = new TMDB_Suggestion { TmdbEntityType = MetadataEntityType.Movie, TmdbEntityID = 4, SuggestedTmdbEntityID = 5 };

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "4"), suggestion.BaseID);
        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "5"), suggestion.SuggestedID);
    }

    #endregion
}
