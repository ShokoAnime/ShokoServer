using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
///   Writes AniDB episodes whose pooled columns repeat, reloads them from the
///   migrated database and checks the values read back.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PooledStringRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 990_401;

    [Fact]
    public void PooledColumnsReadBackAsWrittenAndShareOneInstance()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var episodes = fixture.Services.GetRequiredService<AniDB_EpisodeRepository>();
        Clean(episodes);
        try
        {
            episodes.Save([Episode(1, "7.25", "12"), Episode(2, "7.25", "12"), Episode(3, "0", "0")]);

            episodes.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            var stored = episodes.GetByAnimeID(AnimeID).OrderBy(episode => episode.EpisodeNumber).ToList();

            Assert.Equal(["7.25", "7.25", "0"], stored.Select(episode => episode.Rating));
            Assert.Equal(["12", "12", "0"], stored.Select(episode => episode.Votes));
            Assert.Same(stored[0].Rating, stored[1].Rating);
            Assert.Same(stored[0].Votes, stored[1].Votes);

            // Saving a reloaded row writes the same values back.
            stored[2].Rating = "8.5";
            episodes.Save(stored[2]);
            episodes.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["7.25", "7.25", "8.5"], episodes.GetByAnimeID(AnimeID).OrderBy(episode => episode.EpisodeNumber).Select(episode => episode.Rating));
        }
        finally
        {
            Clean(episodes);
        }
    }

    private static AniDB_Episode Episode(int number, string rating, string votes)
        => new()
        {
            EpisodeID = AnimeID * 10 + number,
            AnimeID = AnimeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = number,
            Description = string.Empty,
            Rating = new string(rating.AsSpan()),
            Votes = new string(votes.AsSpan()),
            CreatedAt = new DateTime(2020, 1, 2),
            DateTimeUpdated = new DateTime(2020, 1, 2),
        };

    private static void Clean(AniDB_EpisodeRepository episodes)
        => episodes.Delete(episodes.GetByAnimeID(AnimeID));
}
