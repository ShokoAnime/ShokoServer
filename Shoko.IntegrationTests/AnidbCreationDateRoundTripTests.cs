using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Xunit;

using HttpXmlUtils = Shoko.Server.Providers.AniDB.HttpXmlUtils;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes AniDB anime and episodes with and without a creation date into the
/// migrated database, fills the missing ones and reads them back, so the new
/// columns and the fix are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AnidbCreationDateRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int StampedAnimeID = 990_301;

    private const int CachedAnimeID = 990_302;

    private const int UpdatedAnimeID = 990_303;

    // What the schema step gives the rows stored before the column.
    private static readonly DateTime _unset = new(1970, 1, 1);

    private static readonly DateTime _createdAt = new(2020, 1, 2, 3, 4, 5);

    private static readonly DateTime _xmlWrittenAt = new(2021, 6, 7, 8, 9, 10);

    private static readonly DateTime _animeUpdatedAt = new(2022, 11, 12, 13, 14, 15);

    private static readonly DateTime _episodeUpdatedAt = new(2019, 4, 5, 6, 7, 8);

    [Fact]
    public void TheCreationDatesReadBackAsWrittenAndTheMissingOnesAreFilled()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var anime = services.GetRequiredService<AniDB_AnimeRepository>();
        var episodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var xmlPath = Path.Combine(services.GetRequiredService<HttpXmlUtils>().AnimeXmlDirectory, $"AnimeDoc_{CachedAnimeID}.xml");
        Clean(anime, episodes, xmlPath);
        try
        {
            anime.Save([Anime(StampedAnimeID, _createdAt), Anime(CachedAnimeID, _unset), Anime(UpdatedAnimeID, _unset)]);
            episodes.Save([Episode(StampedAnimeID, _createdAt), Episode(CachedAnimeID, _unset), Episode(UpdatedAnimeID, _unset)]);
            Directory.CreateDirectory(Path.GetDirectoryName(xmlPath)!);
            File.WriteAllText(xmlPath, "<anime />");
            File.SetLastWriteTime(xmlPath, _xmlWrittenAt);

            DatabaseFixes.BackfillAnidbCreationDates();
            anime.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            episodes.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                [_createdAt, _xmlWrittenAt, _animeUpdatedAt],
                new[] { StampedAnimeID, CachedAnimeID, UpdatedAnimeID }.Select(id => anime.GetByAnimeID(id)!.CreatedAt)
            );
            Assert.Equal(
                [_createdAt, _xmlWrittenAt, _animeUpdatedAt],
                new[] { StampedAnimeID, CachedAnimeID, UpdatedAnimeID }.Select(id => episodes.GetByEpisodeID(id + 1_000)!.CreatedAt)
            );
        }
        finally
        {
            Clean(anime, episodes, xmlPath);
        }
    }

#pragma warning disable CS0618
    private static AniDB_Anime Anime(int animeID, DateTime createdAt)
        => new()
        {
            AnimeID = animeID,
            MainTitle = $"Anime {animeID}",
            AnimeType = AnimeType.TVSeries,
            Description = string.Empty,
            AllTags = string.Empty,
            CreatedAt = createdAt,
            DateTimeUpdated = _animeUpdatedAt,
            DateTimeDescUpdated = _animeUpdatedAt,
        };
#pragma warning restore CS0618

    private static AniDB_Episode Episode(int animeID, DateTime createdAt)
        => new()
        {
            EpisodeID = animeID + 1_000,
            AnimeID = animeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = 1,
            Description = string.Empty,
            CreatedAt = createdAt,
            DateTimeUpdated = _episodeUpdatedAt,
        };

    private static void Clean(AniDB_AnimeRepository anime, AniDB_EpisodeRepository episodes, string xmlPath)
    {
        foreach (var animeID in new[] { StampedAnimeID, CachedAnimeID, UpdatedAnimeID })
        {
            episodes.Delete(episodes.GetByAnimeID(animeID));
            if (anime.GetByAnimeID(animeID) is { } row)
                anime.Delete(row);
        }

        if (File.Exists(xmlPath))
            File.Delete(xmlPath);
    }
}
