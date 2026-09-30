using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Xunit;
using static Shoko.IntegrationTests.Sql;

#pragma warning disable CS0618
namespace Shoko.IntegrationTests;

/// <summary>
/// Seeds the rows the metadata identity steps rewrite, runs the steps again
/// against the migrated database, and reads the rows back, so the SQL of each
/// backend and the data fix are checked on real rows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataIdentityDataFixTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static ShokoImage_Entity CrossReference(MetadataSource entitySource, MetadataEntityType entityType, string entityID)
        => new()
        {
            ImageID = Guid.NewGuid(),
            PrimaryImageID = Guid.NewGuid(),
            ImageType = ImageEntityType.Primary,
            ImageSource = MetadataSource.User,
            EntitySource = entitySource,
            EntityType = entityType,
            EntityID = entityID,
            IsEnabled = true,
            Source = MetadataSource.User,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

    #endregion

    #region Link Steps

    [Fact]
    public void TheLinkCopyStepsSkipATmdbZeroSeriesOrMovie_ButKeepAnEpisodeZeroAsEmpty()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        // The steps copying the series, movie and episode links.
        var steps = SchemaSteps.Get(fixture, 23, 29, 35);

        using var connection = fixture.OpenConnection();
        try
        {
            // What a database upgraded from a released version holds until the drop steps run.
            ReleasedTextSchema.Restore(connection, fixture.Backend);

            // Released builds could store a link to ID 0, and a link twice: only the first of each
            // real link is copied, and the orderings count only what is copied.
            foreach (var (animeID, showID) in new[] { (991101, 0), (991101, 42), (991101, 0), (991101, 43), (991101, 42), (991102, 0), (991103, 44) })
                Execute(connection, $"INSERT INTO CrossRef_AniDB_TMDB_Show (AnidbAnimeID, TmdbShowID, MatchRating) VALUES ({animeID}, {showID}, 1)");
            foreach (var (episodeID, movieID) in new[] { (5, 0), (5, 77), (5, 0), (5, 78), (6, 42) })
                Execute(connection, "INSERT INTO CrossRef_AniDB_TMDB_Movie (AnidbAnimeID, AnidbEpisodeID, TmdbMovieID, MatchRating) " +
                    $"VALUES (991101, {episodeID}, {movieID}, 1)");

            // An episode link to nothing is a real refusal, and is kept.
            Execute(connection, "INSERT INTO CrossRef_AniDB_TMDB_Episode (AnidbAnimeID, AnidbEpisodeID, TmdbShowID, TmdbEpisodeID, Ordering, MatchRating) " +
                "VALUES (991101, 5, 0, 0, 0, 1)");
            Execute(connection, "INSERT INTO CrossRef_AniDB_TMDB_Episode (AnidbAnimeID, AnidbEpisodeID, TmdbShowID, TmdbEpisodeID, Ordering, MatchRating) " +
                "VALUES (991101, 6, 42, 4242, 0, 1)");

            foreach (var step in steps)
                Execute(connection, step.Command!);

            Assert.Equal(
                ["991101|42|0", "991101|43|1", "991103|44|0"],
                Read(connection, "SELECT AnidbAnimeID, ProviderID, Ordering FROM CrossRef_AniDB_Metadata_Series WHERE Source = 1 " +
                    "AND AnidbAnimeID IN (991101, 991102, 991103) ORDER BY AnidbAnimeID, Ordering"));
            Assert.Equal(
                ["5|77|0", "5|78|1", "6|42|0"],
                Read(connection, "SELECT AnidbEpisodeID, ProviderID, Ordering FROM CrossRef_AniDB_Metadata_Movie WHERE Source = 1 AND AnidbAnimeID = 991101 " +
                    "ORDER BY AnidbEpisodeID, Ordering"));
            Assert.Equal(
                ["5||", "6|4242|42"],
                Read(connection, "SELECT AnidbEpisodeID, ProviderID, ProviderParentID FROM CrossRef_AniDB_Metadata_Episode WHERE Source = 1 AND AnidbAnimeID = 991101 " +
                    "ORDER BY AnidbEpisodeID"));
        }
        finally
        {
            ReleasedTextSchema.Drop(fixture, connection);
            foreach (var table in new[] { "CrossRef_AniDB_Metadata_Series", "CrossRef_AniDB_Metadata_Movie", "CrossRef_AniDB_Metadata_Episode" })
                Execute(connection, $"DELETE FROM {table} WHERE Source = 1 AND AnidbAnimeID IN (991101, 991102, 991103)");
        }
    }

    #endregion

    #region Season Image Steps

    [Fact]
    public void TheSeasonStepsMoveAShokoSeasonToShoko_BeforeRetypingAnAnidbSeason()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        // The step moving a Shoko season, then the one retyping an AniDB season.
        var steps = SchemaSteps.Get(fixture, 69, 70);
        var xrefs = fixture.Services.GetRequiredService<ShokoImage_EntityRepository>();

        // Older builds stored a Shoko season under AniDB with the season kind,
        // and an AniDB season under AniDB with no kind at all.
        var shokoSeason = CrossReference(MetadataSource.AniDB, MetadataEntityType.Season, "990012:Episode:1");
        var anidbSeason = CrossReference(MetadataSource.AniDB, MetadataEntityType.Season, "990013:Episode:1");
        xrefs.Save([shokoSeason, anidbSeason]);
        using var connection = fixture.OpenConnection();
        try
        {
            Execute(connection, $"UPDATE ShokoImage_Entity SET EntityType = 0 WHERE ID = {anidbSeason.ID}");

            foreach (var step in steps)
                Execute(connection, step.Command!);
            xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            var movedShoko = xrefs.GetByID(shokoSeason.ID);
            Assert.NotNull(movedShoko);
            Assert.Equal(new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Season, "990012:Episode:1"), movedShoko.GetEntityID());
            var movedAnidb = xrefs.GetByID(anidbSeason.ID);
            Assert.NotNull(movedAnidb);
            Assert.Equal(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Season, "990013:Episode:1"), movedAnidb.GetEntityID());
        }
        finally
        {
            Execute(connection, $"DELETE FROM ShokoImage_Entity WHERE ID IN ({shokoSeason.ID}, {anidbSeason.ID})");
            xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        }
    }

    #endregion

    #region Image Cross-Reference Fix

    [Fact]
    public void TheImageFixKeysAVideoByItsHash_AndASecondRunChangesNothing()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var xrefs = fixture.Services.GetRequiredService<ShokoImage_EntityRepository>();
        var videos = fixture.Services.GetRequiredService<VideoLocalRepository>();

        var video = new VideoLocal
        {
            Hash = $"{Guid.NewGuid():N}".ToUpperInvariant(),
            FileSize = 734003200,
            FileName = "video.mkv",
            DateTimeCreated = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
        };
        videos.Save(video, false);

        var videoXref = CrossReference(MetadataSource.Shoko, MetadataEntityType.Video, video.VideoLocalID.ToString());
        var seeded = new[] { videoXref };
        xrefs.Save(seeded);
        try
        {
            for (var run = 0; run < 2; run++)
            {
                DatabaseFixes.MoveImageCrossReferencesToEntityIDs();
                xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal($"{video.Hash}+{video.FileSize}", xrefs.GetByID(videoXref.ID)?.EntityID);
            }
        }
        finally
        {
            xrefs.Delete(seeded.Select(xref => xrefs.GetByID(xref.ID)).OfType<ShokoImage_Entity>().ToList());
            videos.Delete(video);
        }
    }

    #endregion
}
