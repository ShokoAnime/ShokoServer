using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Web.SignalR;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the per-user sends of <see cref="EventEmitter"/>.
/// </summary>
public class EventEmitterSendTests
{
    [Fact]
    public async Task SendWhereAsync_SendsOnlyToTheConnectionsOfMatchingUsers()
    {
        var hub = new RecordingHub();
        var feed = new TestFeed(hub.Untyped);
        await feed.ConnectAsync("a1", User(1));
        await feed.ConnectAsync("a2", User(1));
        await feed.ConnectAsync("a3", User(1));
        await feed.ConnectAsync("b1", User(2));
        await feed.DisconnectAsync("a3");

        await feed.SendWhere(user => user.LocalID == 1, "ping", 42);

        var message = Assert.Single(hub.Sent);
        Assert.Equal(["a1", "a2"], message.ConnectionIDs.Order());
        Assert.Equal([42], message.Args);
    }

    [Fact]
    public async Task SendPerUserAsync_SharesOneSendPerArgumentsAndSkipsNull()
    {
        var hub = new RecordingHub();
        var feed = new TestFeed(hub.Untyped);
        for (var userID = 1; userID <= 4; userID++)
            await feed.ConnectAsync("c" + userID, User(userID));
        object[] shared = ["shared"];

        await feed.SendPerUser("ping", user => user.LocalID switch
        {
            1 or 2 => shared,
            3 => ["own"],
            _ => null,
        });

        Assert.Equal(2, hub.Sent.Count);
        Assert.Equal(["c1", "c2"], hub.Sent.Single(message => ReferenceEquals(message.Args, shared)).ConnectionIDs.Order());
        Assert.Equal(["c3"], hub.Sent.Single(message => !ReferenceEquals(message.Args, shared)).ConnectionIDs);
    }

    private static IUser User(int userID)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.LocalID).Returns(userID);
        return user.Object;
    }

    private sealed class TestFeed : EventEmitter
    {
        public TestFeed(IHubContext hub)
            => Attach(hub);

        public override string Name => "test";

        public Task SendWhere(Func<IUser, bool> predicate, string subject, params object[] args)
            => SendWhereAsync(predicate, subject, args);

        public Task SendPerUser(string subject, Func<IUser, object[]?> getArgs)
            => SendPerUserAsync(subject, getArgs);
    }
}
