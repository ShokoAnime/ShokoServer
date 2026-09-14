using System.Linq;
using Xunit;

using Fixtures = Shoko.TestData.TestData;

namespace Shoko.Tests;

/// <summary>
/// Guards the shared fixtures in <see cref="Fixtures"/>. Each accessor pulls a different embedded
/// resource, and because the two accessors are otherwise identical it is easy for one to end up
/// reading the other's file — which yields a silently empty or nonsensical collection rather than
/// an error.
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

    [Fact]
    public void CrossRefFileEpisode_LoadsPopulatedRecords()
    {
        var crossRefs = Fixtures.CrossRef_File_Episode.Value.ToList();

        Assert.NotEmpty(crossRefs);
        // Reading the wrong resource still deserialises, but every field comes back at its default.
        Assert.All(crossRefs, x => Assert.NotEqual(0, x.EpisodeID));
        Assert.All(crossRefs, x => Assert.False(string.IsNullOrEmpty(x.Hash)));
    }
}
