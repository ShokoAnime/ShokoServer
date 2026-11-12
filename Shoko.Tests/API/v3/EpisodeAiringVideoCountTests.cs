using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Video;
using Shoko.Server.API.v3.Models.Airing;
using Xunit;

namespace Shoko.Tests.API.v3;

/// <summary>
///   A calendar row counts the files of the AniDB episode it airs and those
///   linked directly to the schedule's own episode of another source, each
///   once.
/// </summary>
public class EpisodeAiringVideoCountTests
{
    private static IVideo Video(int id)
        => Mock.Of<IVideo>(video => video.ID == new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Video, id.ToString()));

    [Fact]
    public void AnAiring_CountsTheAnidbEpisodesFiles_AndThoseOfItsOwnEpisode_EachOnce()
    {
        var anidbEpisode = new Mock<IAnidbEpisode>();
        anidbEpisode.SetupGet(e => e.Videos).Returns([Video(1), Video(2)]);
        var scheduled = new Mock<IEpisode>();
        scheduled.SetupGet(e => e.Videos).Returns([Video(2), Video(3)]);
        var airing = new Mock<IEpisodeAiring> { DefaultValue = DefaultValue.Mock };
        airing.SetupGet(a => a.AnidbEpisode).Returns(anidbEpisode.Object);
        airing.SetupGet(a => a.Episode).Returns(scheduled.Object);

        Assert.Equal(3, new EpisodeAiring(airing.Object).VideoCount);
    }

    [Fact]
    public void AnAiringOfAnotherSourceOnly_CountsItsOwnEpisodesFiles()
    {
        var scheduled = new Mock<IEpisode>();
        scheduled.SetupGet(e => e.Videos).Returns([Video(5)]);
        var airing = new Mock<IEpisodeAiring> { DefaultValue = DefaultValue.Mock };
        airing.SetupGet(a => a.AnidbEpisode).Returns((IAnidbEpisode?)null);
        airing.SetupGet(a => a.ShokoEpisode).Returns((Shoko.Abstractions.Metadata.Shoko.IShokoEpisode?)null);
        airing.SetupGet(a => a.Episode).Returns(scheduled.Object);

        Assert.Equal(1, new EpisodeAiring(airing.Object).VideoCount);
    }
}
