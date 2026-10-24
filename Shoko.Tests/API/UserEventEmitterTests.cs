using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.Infrastructure.TestViewers;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the API token events <see cref="UserEventEmitter"/> forwards to the
/// user feed.
/// </summary>
public class UserEventEmitterTests
{
    [Fact]
    public async Task ApiTokenEvents_GoOnlyToTheOwnerAndTheAdmins()
    {
        var hub = new RecordingHub();
        var users = new Mock<IUserService>();
        var emitter = new UserEventEmitter(hub.Typed, users.Object, NullLogger<UserEventEmitter>.Instance);
        var owner = Restricted(userID: 1);
        await emitter.ConnectAsync("owner", owner);
        await emitter.ConnectAsync("admin", Restricted(userID: 3, isAdmin: true));
        await emitter.ConnectAsync("other", Restricted(userID: 4));

        users.Raise(u => u.ApiTokenInvalidated += null, new ApiTokenChangedEventArgs
        {
            ApiToken = new ApiToken(owner, "desktop", "secret", null),
            OccurredAt = DateTime.UnixEpoch,
        });

        Assert.Equal(["admin", "owner"], hub.ReceivedBy("user:apiToken.invalidated"));
    }
}
