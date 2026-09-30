using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Server.API.SignalR.Models;
using Xunit;

namespace Shoko.Tests.API;

public class RestartEventEmitterTests
{
    private static readonly RestartReason _reason = new()
    {
        Source = RestartReasonSource.Plugin,
        PluginID = Guid.Parse("0f3f8d6e-2b3a-4c55-9d2e-7a1b4c5d6e7f"),
        Key = "sources",
        Description = "The enabled sources changed.",
        RaisedAt = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task ConnectAsync_SendsTheCurrentReasonsToAnAdmin()
    {
        var harness = new Harness();

        Assert.True(await harness.Emitter.ConnectAsync("admin-connection", User(isAdmin: true)));

        var (_, args) = Assert.Single(harness.Sent);
        var model = Assert.IsType<RestartReasonsSignalRModel>(Assert.Single(args));
        Assert.True(model.RestartRequired);
        Assert.Equal("sources", Assert.Single(model.Reasons).Key);
        harness.Groups.Verify(groups => groups.AddToGroupAsync("admin-connection", harness.Emitter.Name, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_RefusesAndSendsNothingToAUserWhoIsNotAnAdmin()
    {
        var harness = new Harness();

        Assert.False(await harness.Emitter.ConnectAsync("user-connection", User(isAdmin: false)));
    }

    [Fact]
    public void RestartReasonsChanged_IsSentToTheFeedGroup()
    {
        var harness = new Harness();

        harness.SystemService.Raise(service => service.RestartReasonsChanged += null, new RestartReasonsChangedEventArgs { Reasons = [] });

        var (_, args) = Assert.Single(harness.GroupSent);
        var model = Assert.IsType<RestartReasonsSignalRModel>(Assert.Single(args));
        Assert.False(model.RestartRequired);
        Assert.Empty(model.Reasons);
    }

    private static IUser User(bool isAdmin)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.IsAdmin).Returns(isAdmin);
        user.Setup(u => u.LocalID).Returns(isAdmin ? 1 : 2);
        return user.Object;
    }

    private sealed class Harness
    {
        public Mock<ISystemService> SystemService { get; } = new();

        public Mock<IGroupManager> Groups { get; } = new();

        public List<(string Method, object?[] Args)> Sent { get; } = [];

        public List<(string Method, object?[] Args)> GroupSent { get; } = [];

        public RestartEventEmitter Emitter { get; }

        public Harness()
        {
            SystemService.Setup(service => service.RestartReasons).Returns([_reason]);

            var client = new Mock<ISingleClientProxy>();
            client.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string method, object?[] args, CancellationToken _) => Sent.Add((method, args)))
                .Returns(Task.CompletedTask);
            var group = new Mock<IClientProxy>();
            group.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string method, object?[] args, CancellationToken _) => GroupSent.Add((method, args)))
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Client(It.IsAny<string>())).Returns(client.Object);
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
            var hub = new Mock<IHubContext<AggregateHub>>();
            hub.Setup(h => h.Clients).Returns(clients.Object);
            hub.Setup(h => h.Groups).Returns(Groups.Object);

            var pluginManager = new Mock<IPluginManager>();
            Emitter = new(hub.Object, SystemService.Object, pluginManager.Object, NullLogger<RestartEventEmitter>.Instance);
        }
    }
}
