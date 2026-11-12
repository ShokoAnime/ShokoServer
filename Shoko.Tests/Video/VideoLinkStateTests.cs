using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Video;

/// <summary>
///   When a recognised video is in import limbo, and which relocation
///   providers take a video by how it is linked.
/// </summary>
public class VideoLinkStateTests
{
    private static readonly MetadataGuid _episode = new(MetadataSource.TMDB, MetadataEntityType.Episode, "1");

    private static readonly MetadataGuid _anidbEpisode = new(MetadataSource.AniDB, MetadataEntityType.Episode, "3");

    [Theory]
    [InlineData("tmdb://episode/1,tmdb://series/2", "tmdb://episode/1,tmdb://series/2", false)]
    [InlineData("tmdb://episode/1,tmdb://series/2", "tmdb://series/2", true)]
    [InlineData("tmdb://episode/1,tmdb://series/2", "tmdb://episode/1", true)]
    [InlineData("tmdb://episode/1", "tmdb://episode/1", false)]
    [InlineData("anidb://episode/3", "", false)]
    [InlineData("", "", false)]
    public void ALinkOfAnotherSource_IsInLimboUntilItsMainEntryAndTheParentItNamesResolve(string named, string resolvable, bool inLimbo)
    {
        var ids = Parse(named);
        var available = Parse(resolvable).ToHashSet();
        IReleaseVideoCrossReference[] crossReferences = ids.Count is 0 ? [] : [ReleaseVideoCrossReference.For(ids)];

        var result = VideoLocal.IsInImportLimboFor([], crossReferences, id => available.Contains(id) ? Mock.Of<IMetadata>() : null);

        Assert.Equal(inLimbo, result);
    }

    [Fact]
    public void AnAnidbLink_IsLeftToItsLinkRows_AndNeverResolved()
    {
        var asked = new List<MetadataGuid>();

        var result = VideoLocal.IsInImportLimboFor([], [ReleaseVideoCrossReference.For([_anidbEpisode, _episode])], id =>
        {
            asked.Add(id);
            return null;
        });

        Assert.False(result);
        Assert.Empty(asked);
    }

    [Theory]
    // Linked to AniDB: every provider.
    [InlineData(true, true, false, false, true)]
    // Unrecognized: only with SupportsUnrecognized.
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, false)]
    // Linked only to other sources: only with SupportsNonAnidbLinkedVideos.
    [InlineData(false, true, true, false, false)]
    [InlineData(false, true, false, true, true)]
    public void ARelocationProvider_TakesAVideoByHowItIsLinked(bool anidbLinked, bool recognized, bool supportsUnrecognized, bool supportsNonAnidbLinked, bool takes)
    {
        var video = new Mock<IVideo>();
        video.SetupGet(v => v.CrossReferences).Returns(anidbLinked ? [Mock.Of<IVideoCrossReference>()] : []);
        video.SetupGet(v => v.IsRecognized).Returns(recognized);

        Assert.Equal(takes, VideoRelocationService.CanHandle(supportsUnrecognized, supportsNonAnidbLinked, video.Object));
    }

    private static List<MetadataGuid> Parse(string ids)
        => [.. ids.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(id => MetadataGuid.Parse(id))];
}
