using System.Collections.Generic;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

/// <summary>
/// Unit tests for the <see cref="CrossReferenceIDs"/> constants,
/// <see cref="ReleaseVideoCrossReferenceExtensions.ForAniDB"/> extension, and the
/// <see cref="ReleaseVideoCrossReferenceExtensions"/> helpers.
/// </summary>
public class ReleaseVideoCrossReferenceTests
{
    // ── ForAniDB factory ──────────────────────────────────────────────────────

    [Fact]
    public void ForAniDB_WithAnimeID_SetsBothProviderIDs()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, 69);

        Assert.Equal("132456", xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.Equal("69", xref.ProviderIDs[CrossReferenceIDs.AniDB_Anime]);
    }

    [Fact]
    public void ForAniDB_WithoutAnimeID_SetsOnlyEpisodeID()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, null);

        Assert.Equal("132456", xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.False(xref.ProviderIDs.ContainsKey(CrossReferenceIDs.AniDB_Anime));
    }

    [Fact]
    public void ForAniDB_WithoutPercentages_DefaultsToFullRange()
    {
        var xref = (IReleaseVideoCrossReference)ReleaseVideoCrossReference.ForAniDB(1, 1);

        Assert.Equal(0, xref.PercentageStart);
        Assert.Equal(100, xref.PercentageEnd);
    }

    // ── GetAnidbEpisodeID extension ───────────────────────────────────────────

    [Theory]
    [InlineData("132456", 132456)]
    [InlineData(null, 0)] // No key stored.
    [InlineData("not-a-number", 0)]
    public void GetAnidbEpisodeID_ReturnsZero_UnlessTheStoredValueIsNumeric(string? storedValue, int expected)
    {
        var xref = new ReleaseVideoCrossReference();
        if (storedValue is not null)
            xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = storedValue;

        Assert.Equal(expected, xref.AnidbEpisodeID);
    }

    // ── GetAnidbAnimeID extension ─────────────────────────────────────────────

    [Theory]
    [InlineData("69", 69)]
    [InlineData(null, null)] // No key stored.
    [InlineData("xyz", null)]
    public void GetAnidbAnimeID_ReturnsNull_UnlessTheStoredValueIsNumeric(string? storedValue, int? expected)
    {
        var xref = new ReleaseVideoCrossReference();
        if (storedValue is not null)
            xref.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = storedValue;

        Assert.Equal(expected, xref.AnidbAnimeID);
    }

    // ── EmbeddedCrossReference ────────────────────────────────────────────────

    [Fact]
    public void EmbeddedCrossReference_ProviderIDs_StoresAndReturnsEpisodeID()
    {
        var ecr = new EmbeddedCrossReference();
        ecr.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = "5001";

        Assert.Equal("5001", ecr.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.Equal(5001, ecr.AnidbEpisodeID);
    }

    [Fact]
    public void EmbeddedCrossReference_CopyConstructor_ClonesProviderIDs()
    {
        var original = new EmbeddedCrossReference();
        original.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = "42";
        original.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = "7";

        var copy = new EmbeddedCrossReference(original);

        Assert.Equal("42", copy.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.Equal("7", copy.ProviderIDs[CrossReferenceIDs.AniDB_Anime]);
    }

    [Fact]
    public void EmbeddedCrossReference_CopyConstructor_ProviderIDsAreIndependent()
    {
        var original = new EmbeddedCrossReference();
        original.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = "42";

        var copy = new EmbeddedCrossReference(original);
        copy.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = "99";

        Assert.Equal("42", original.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
    }

    // ── ReleaseVideoCrossReference copy constructor ───────────────────────────

    [Fact]
    public void CopyConstructor_FromInterface_ClonesProviderIDs()
    {
        var original = ReleaseVideoCrossReference.ForAniDB(132456, 69, 0, 50);
        IReleaseVideoCrossReference iface = original;

        var copy = new ReleaseVideoCrossReference(iface);

        Assert.Equal("132456", copy.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.Equal("69", copy.ProviderIDs[CrossReferenceIDs.AniDB_Anime]);
        Assert.Equal(0, ((IReleaseVideoCrossReference)copy).PercentageStart);
        Assert.Equal(50, ((IReleaseVideoCrossReference)copy).PercentageEnd);
    }
}
