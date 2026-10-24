using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.Infrastructure.TestViewers;

namespace Shoko.Tests.API;

/// <summary>
/// The <c>action</c> and <c>relocation</c> feeds take admins only.
/// </summary>
public class AdminFeedTests
{
    [Fact]
    public async Task TheActionFeed_RefusesUsersWhoAreNotAdmins()
    {
        var emitter = new ActionEventEmitter(new RecordingHub().Typed, Mock.Of<IScheduledActionService>(), NullLogger<ActionEventEmitter>.Instance);

        Assert.False(await emitter.ConnectAsync("user", Restricted(userID: 4)));
        Assert.True(await emitter.ConnectAsync("admin", Restricted(userID: 3, isAdmin: true)));
    }

    [Fact]
    public async Task TheRelocationFeed_RefusesUsersWhoAreNotAdmins()
    {
        var emitter = new RelocationEventEmitter(new RecordingHub().Typed, Mock.Of<IRelocationPresetManager>(), NullLogger<RelocationEventEmitter>.Instance);

        Assert.False(await emitter.ConnectAsync("user", Restricted(userID: 4)));
        Assert.True(await emitter.ConnectAsync("admin", Restricted(userID: 3, isAdmin: true)));
    }
}
