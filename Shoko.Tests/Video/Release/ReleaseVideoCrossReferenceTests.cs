#nullable enable
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

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 100)]
    [InlineData(0, 50)]
    public void ForAniDB_WithPercentages_SetsCorrectRange(int start, int end)
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(1, 1, start, end);

        Assert.Equal(start, ((IReleaseVideoCrossReference)xref).PercentageStart);
        Assert.Equal(end, ((IReleaseVideoCrossReference)xref).PercentageEnd);
    }

    [Fact]
    public void ForAniDB_WithoutPercentages_DefaultsToFullRange()
    {
        var xref = (IReleaseVideoCrossReference)ReleaseVideoCrossReference.ForAniDB(1, 1);

        Assert.Equal(0, xref.PercentageStart);
        Assert.Equal(100, xref.PercentageEnd);
    }

    // ── GetAnidbEpisodeID extension ───────────────────────────────────────────

    [Fact]
    public void GetAnidbEpisodeID_WithValidKey_ReturnsID()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, 69);

        Assert.Equal(132456, xref.AnidbEpisodeID);
    }

    [Fact]
    public void GetAnidbEpisodeID_MissingKey_ReturnsNull()
    {
        var xref = new ReleaseVideoCrossReference();

        Assert.Null(xref.AnidbEpisodeID);
    }

    [Fact]
    public void GetAnidbEpisodeID_NonNumericValue_ReturnsNull()
    {
        var xref = new ReleaseVideoCrossReference();
        xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = "not-a-number";

        Assert.Null(xref.AnidbEpisodeID);
    }

    // ── GetAnidbAnimeID extension ─────────────────────────────────────────────

    [Fact]
    public void GetAnidbAnimeID_WithValidKey_ReturnsID()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, 69);

        Assert.Equal(69, xref.AnidbAnimeID);
    }

    [Fact]
    public void GetAnidbAnimeID_MissingKey_ReturnsNull()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, null);

        Assert.Null(xref.AnidbAnimeID);
    }

    [Fact]
    public void GetAnidbAnimeID_NonNumericValue_ReturnsNull()
    {
        var xref = new ReleaseVideoCrossReference();
        xref.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = "xyz";

        Assert.Null(xref.AnidbAnimeID);
    }

    // ── custom provider keys ──────────────────────────────────────────────────

    [Fact]
    public void ProviderIDs_AcceptsArbitraryKeys()
    {
        const string customKey = "MyProvider_ContentID";
        var xref = ReleaseVideoCrossReference.ForAniDB(1, 1);
        xref.ProviderIDs[customKey] = "abc-123";

        Assert.Equal("abc-123", xref.ProviderIDs[customKey]);
        Assert.Equal(3, xref.ProviderIDs.Count); // AniDB_Episode + AniDB_Anime + custom
    }

    // ── IReleaseVideoCrossReference explicit implementation ───────────────────

    [Fact]
    public void ExplicitInterface_ProviderIDsIsReadOnly()
    {
        var xref = ReleaseVideoCrossReference.ForAniDB(132456, 69);
        IReleaseVideoCrossReference iface = xref;

        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(iface.ProviderIDs);
        Assert.Equal("132456", iface.ProviderIDs[CrossReferenceIDs.AniDB_Episode]);
        Assert.Equal("69", iface.ProviderIDs[CrossReferenceIDs.AniDB_Anime]);
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
