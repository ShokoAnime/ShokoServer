using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes AniDB resource rows into the migrated database and reads them back
/// from it, so the new table and its value lists are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AnidbResourceRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 990_101;

    private const int EpisodeID = 990_102;

    [Fact]
    public void TheRowsOfAnAnimeAndItsEpisodesReadBackAsWritten()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var repository = fixture.Services.GetRequiredService<AniDB_ResourceRepository>();
        repository.Delete(repository.GetAllByAnimeID(AnimeID));
        repository.Save(
        [
            new() { AnimeID = AnimeID, ResourceType = ResourceLinkType.TMDB, Ordering = 0, Identifiers = ["139161", "tv"] },
            new() { AnimeID = AnimeID, ResourceType = ResourceLinkType.OfficialStream, Ordering = 1, Urls = ["https://www.bilibili.tv/en/media/2072897/"] },
            new() { AnimeID = AnimeID, ResourceType = (ResourceLinkType)31, Ordering = 2, Identifiers = ["12339"] },
            new() { AnimeID = AnimeID, ResourceType = ResourceLinkType.Wiki_JP, Ordering = 3, Identifiers = ["齢5000年の草食ドラゴン、いわれなき邪竜認定"] },
            new() { AnimeID = AnimeID, EpisodeID = EpisodeID, ResourceType = ResourceLinkType.Crunchyroll, Ordering = 0, Identifiers = ["G2XU0Q9EN"] },
            new() { AnimeID = AnimeID, EpisodeID = EpisodeID, ResourceType = ResourceLinkType.Crunchyroll, Ordering = 1, Identifiers = ["G8WUN89G9"] },
        ]);

        repository.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

        var anime = repository.GetByAnimeID(AnimeID);
        Assert.Equal(
            [
                (ResourceLinkType.TMDB, "139161|tv", ""),
                (ResourceLinkType.OfficialStream, "", "https://www.bilibili.tv/en/media/2072897/"),
                ((ResourceLinkType)31, "12339", ""),
                (ResourceLinkType.Wiki_JP, "齢5000年の草食ドラゴン、いわれなき邪竜認定", ""),
            ],
            anime.Select(row => (row.ResourceType, string.Join('|', row.Identifiers), string.Join('|', row.Urls)))
        );
        Assert.All(anime, row => Assert.Null(row.EpisodeID));
        Assert.Equal(["G2XU0Q9EN", "G8WUN89G9"], repository.GetByEpisodeID(EpisodeID).SelectMany(row => row.Identifiers));
        Assert.Equal(6, repository.GetAllByAnimeID(AnimeID).Count);

        repository.Delete(repository.GetAllByAnimeID(AnimeID));
    }
}
