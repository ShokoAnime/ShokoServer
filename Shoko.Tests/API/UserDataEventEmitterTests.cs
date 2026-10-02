using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.Infrastructure.TestViewers;

namespace Shoko.Tests.API;

/// <summary>
/// Covers who the user data feed sends a user's data to.
/// </summary>
public class UserDataEventEmitterTests
{
    [Fact]
    public async Task SeriesSaved_ReachesTheOwnerOfAHiddenSeries_AndNoOtherRestrictedUser()
    {
        var hub = new RecordingHub();
        var userData = new Mock<IUserDataService>();
        var emitter = new UserDataEventEmitter(hub.Typed, userData.Object, AnimeRepository(), NullLogger<UserDataEventEmitter>.Instance);
        var owner = Restricted(userID: 1);
        await emitter.ConnectAsync("owner", owner);
        await emitter.ConnectAsync("other", Restricted(userID: 3));
        await emitter.ConnectAsync("admin", Restricted(userID: 4, isAdmin: true));

        userData.Raise(service => service.SeriesUserDataSaved += null, new SeriesUserDataSavedEventArgs
        {
            VideoReason = default,
            Reason = default,
            User = owner,
            Series = Mock.Of<IShokoSeries>(series => series.AnidbAnimeID == HiddenAnimeID),
            UserData = Mock.Of<ISeriesUserData>(),
        });

        Assert.Equal(["owner"], hub.ReceivedBy("userData:series.saved"));
    }
}
