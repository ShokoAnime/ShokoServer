using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
/// Unit tests for the AniDB helpers of
/// <see cref="ReleaseVideoCrossReferenceExtensions"/> over the typed IDs.
/// </summary>
public class ReleaseVideoCrossReferenceTests
{
    private static readonly MetadataGuid _plugin = new(MetadataSource.TMDB, MetadataEntityType.Episode, "77");

    // ── ForAniDB factory ──────────────────────────────────────────────────────

    [Fact]
    public void ForAniDB_WithAnimeID_PutsTheEpisodeBeforeTheAnime()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, 69);

        Assert.Equal(["anidb://episode/132456", "anidb://series/69"], xref.IDs.ConvertAll(id => id.ToString()));
    }

    [Fact]
    public void ForAniDB_WithoutAnimeID_SetsOnlyEpisodeID()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, null);

        Assert.Equal(["anidb://episode/132456"], xref.IDs.ConvertAll(id => id.ToString()));
        Assert.Null(xref.AnidbAnimeID);
    }

    [Fact]
    public void ForAniDB_WithoutPercentages_DefaultsToFullRange()
    {
        var xref = (IReleaseVideoCrossReference)ReleaseVideoCrossReference.ForAniDB(1, 1);

        Assert.Equal(0, xref.PercentageStart);
        Assert.Equal(100, xref.PercentageEnd);
    }

    // ── Reading the AniDB IDs ─────────────────────────────────────────────────

    [Theory]
    [InlineData("132456", 132456)]
    [InlineData(null, 0)]
    [InlineData("not-a-number", 0)]
    [InlineData("0", 0)]
    public void AnidbEpisodeID_IsZero_UnlessTheIDIsAPositiveNumber(string? id, int expected)
    {
        var xref = new ReleaseVideoCrossReference { IDs = [_plugin] };
        if (id is not null)
            xref.IDs.Add(new(MetadataSource.AniDB, MetadataEntityType.Episode, id));

        Assert.Equal(expected, xref.AnidbEpisodeID);
    }

    [Theory]
    [InlineData("69", 69)]
    [InlineData(null, null)]
    [InlineData("xyz", null)]
    public void AnidbAnimeID_IsNull_UnlessTheIDIsAPositiveNumber(string? id, int? expected)
    {
        var xref = new ReleaseVideoCrossReference { IDs = [_plugin] };
        if (id is not null)
            xref.IDs.Add(new(MetadataSource.AniDB, MetadataEntityType.Series, id));

        Assert.Equal(expected, xref.AnidbAnimeID);
    }

    // ── Setting the AniDB IDs ─────────────────────────────────────────────────

    [Fact]
    public void SettingAnAnidbID_ReplacesItAndKeepsTheOtherSources()
    {
        var xref = new ReleaseVideoCrossReference { IDs = [_plugin] };
        xref.AnidbEpisodeID = 1;
        xref.AnidbAnimeID = 2;
        xref.AnidbEpisodeID = 3;
        xref.AnidbAnimeID = 4;

        Assert.Equal(["anidb://episode/3", "tmdb://episode/77", "anidb://series/4"], xref.IDs.ConvertAll(id => id.ToString()));

        xref.AnidbEpisodeID = 0;
        xref.AnidbAnimeID = null;

        Assert.Equal([_plugin], xref.IDs);
    }

    // ── Copies ────────────────────────────────────────────────────────────────

    [Fact]
    public void CopyConstructor_FromInterface_CopiesTheIDsAndRange()
    {
        var original = ReleaseVideoCrossReference.ForAniDB(132456, 69, 0, 50);

        var copy = new ReleaseVideoCrossReference((IReleaseVideoCrossReference)original);
        copy.AnidbEpisodeID = 99;

        Assert.Equal(132456, original.AnidbEpisodeID);
        Assert.Equal(69, copy.AnidbAnimeID);
        Assert.Equal(0, ((IReleaseVideoCrossReference)copy).PercentageStart);
        Assert.Equal(50, ((IReleaseVideoCrossReference)copy).PercentageEnd);
    }

    [Fact]
    public void EmbeddedCrossReference_SharesPooledIDs()
    {
        var first = new EmbeddedCrossReference(ReleaseVideoCrossReference.ForAniDB(5001, 7));
        var second = new EmbeddedCrossReference(ReleaseVideoCrossReference.ForAniDB(5001, 7));

        Assert.Same(first.IDs[1], second.IDs[1]);
        Assert.Equal(5001, first.AnidbEpisodeID);
    }
}
