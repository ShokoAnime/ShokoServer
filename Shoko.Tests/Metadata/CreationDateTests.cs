using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers where each metadata entry takes its creation date from.
/// </summary>
public class CreationDateTests
{
    private static readonly DateTime _createdAt = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly DateTime _updatedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Fact]
    public void AnAnimeOrEpisodeGivesItsOwnCreationDate()
    {
        var anime = new AniDB_Anime { AnimeID = 1, CreatedAt = _createdAt.ToLocalTime(), DateTimeDescUpdated = _updatedAt.ToLocalTime() };
        var episode = new AniDB_Episode { EpisodeID = 2, AnimeID = 1, CreatedAt = _createdAt.ToLocalTime(), DateTimeUpdated = _updatedAt.ToLocalTime() };

        Assert.Equal(_createdAt, ((ISeries)anime).CreatedAt);
        Assert.Equal(_updatedAt, ((ISeries)anime).LastUpdatedAt);
        Assert.Equal(_createdAt, ((IEpisode)episode).CreatedAt);
    }

    [Fact]
    public void ASeasonBuiltFromItsSeriesTakesTheSeriesDates()
    {
        var anime = new AniDB_Anime { AnimeID = 1, CreatedAt = _createdAt.ToLocalTime(), DateTimeDescUpdated = _updatedAt.ToLocalTime() };
        var series = new AnimeSeries { AnimeSeriesID = 3, DateTimeCreated = _createdAt.ToLocalTime(), DateTimeUpdated = _updatedAt.ToLocalTime() };
        ISeason anidbSeason = new AniDB_Season(anime, EpisodeType.Episode, 1);
        ISeason shokoSeason = new AnimeSeason(series, EpisodeType.Episode, 1);

        Assert.Equal((_createdAt, _updatedAt), (anidbSeason.CreatedAt, anidbSeason.LastUpdatedAt));
        Assert.Equal((_createdAt, _updatedAt), (shokoSeason.CreatedAt, shokoSeason.LastUpdatedAt));
    }

    [Fact]
    public void ACreatorOrCharacterGivesItsOwnCreationDateNotItsLastUpdate()
    {
        IWithCreationDate anidbCreator = new AniDB_Creator { CreatorID = 3, CreatedAt = _createdAt.ToLocalTime(), LastUpdatedAt = _updatedAt };
        IWithCreationDate anidbCharacter = new AniDB_Character { CharacterID = 4, CreatedAt = _createdAt.ToLocalTime(), LastUpdated = _updatedAt };
        IWithCreationDate creator = new Metadata_Creator { CreatedAt = _createdAt, LastUpdatedAt = _updatedAt };
        IWithCreationDate character = new Metadata_Character { CreatedAt = _createdAt, LastUpdatedAt = _updatedAt };

        Assert.All([anidbCreator, anidbCharacter, creator, character], entry => Assert.Equal(_createdAt, entry.CreatedAt));
    }
}
