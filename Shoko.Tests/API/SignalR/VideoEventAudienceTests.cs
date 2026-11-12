using Moq;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.API.SignalR.Aggregate;
using Xunit;

namespace Shoko.Tests.API.SignalR;

/// <summary>
///   A video's events reach the users the API shows the video to: a video
///   linked to no known anime is kept from restricted users.
/// </summary>
public class VideoEventAudienceTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AVideoLinkedToNoAnime_IsHiddenFromRestrictedUsers(bool restricted, bool visible)
    {
        var video = Mock.Of<IVideo>(v => v.CrossReferences == new IVideoCrossReference[0]);
        var user = Mock.Of<IUser>(u => u.RestrictedTags == (restricted ? new IAnidbTag[] { Mock.Of<IAnidbTag>() } : new IAnidbTag[0]));

        Assert.Equal(visible, EventAudience.ForVideo(video, _ => null).IsVisibleTo(user));
    }
}
