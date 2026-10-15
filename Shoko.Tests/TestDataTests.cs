using System.Linq;
using Xunit;

using Fixtures = Shoko.TestData.TestData;

namespace Shoko.Tests;

/// <summary>
/// Guards the <see cref="Fixtures.AniDB_Anime"/> fixture the benchmarks read, which nothing run in CI
/// loads otherwise.
/// </summary>
public class TestDataTests
{
    [Fact]
    public void AniDBAnime_LoadsPopulatedRecords()
    {
        var anime = Fixtures.AniDB_Anime.Value.ToList();

        Assert.NotEmpty(anime);
        Assert.All(anime, a => Assert.NotEqual(0, a.AnimeID));
    }
}
