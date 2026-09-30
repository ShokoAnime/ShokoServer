using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Web.SignalR;
using Shoko.Server.API.SignalR.Aggregate;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the plugin feeds registered in DI on the aggregate hub.
/// </summary>
public class EventEmitterRegistryTests
{
    #region Fixture

    private sealed class SampleFeed : EventEmitter
    {
        public DateTime? LastConnectedAt { get; private set; }

        public override string Name => "sample";

        protected override object[]? GetInitialMessagesForUser(string connectionId, IUser user, DateTime? lastConnectedAt = null)
        {
            LastConnectedAt = lastConnectedAt;
            return [user.LocalID];
        }
    }

    private sealed class AdminFeed : EventEmitter
    {
        public override string Name => "sample.admin";

        protected override bool CanConnect(IUser user) => user.IsAdmin;
    }

    private sealed class OtherSampleFeed : EventEmitter
    {
        public override string Name => "sample";
    }

    private sealed class RecordingLogger : ILogger<EventEmitterRegistry>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Levels.Add(logLevel);
    }

    private sealed class Harness
    {
        public Mock<IGroupManager> Groups { get; } = new();

        public List<(string Target, string Method, object?[] Args)> Sent { get; } = [];

        public RecordingLogger Logger { get; } = new();

        public ServiceProvider Services { get; }

        public EventEmitterRegistry Registry => Services.GetRequiredService<EventEmitterRegistry>();

        public Harness(Action<IServiceCollection> register)
        {
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Client(It.IsAny<string>())).Returns((string id) => Proxy<ISingleClientProxy>(id).Object);
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns((string group) => Proxy<IClientProxy>("group:" + group).Object);
            var hub = new Mock<IHubContext<AggregateHub>>();
            hub.Setup(h => h.Clients).Returns(clients.Object);
            hub.Setup(h => h.Groups).Returns(Groups.Object);

            var services = new ServiceCollection();
            services.AddSingleton(hub.Object);
            services.AddSingleton<ILogger<EventEmitterRegistry>>(Logger);
            services.AddSingleton<EventEmitterRegistry>();
            register(services);
            Services = services.BuildServiceProvider();
        }

        private Mock<T> Proxy<T>(string target) where T : class, IClientProxy
        {
            var proxy = new Mock<T>();
            proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string method, object?[] args, CancellationToken _) => Sent.Add((target, method, args)))
                .Returns(Task.CompletedTask);
            return proxy;
        }
    }

    private static IUser User(int id, bool isAdmin = false)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.LocalID).Returns(id);
        user.Setup(u => u.IsAdmin).Returns(isAdmin);
        return user.Object;
    }

    #endregion

    #region Plugin feeds

    [Fact]
    public async Task PluginFeed_RegisteredInDI_IsListedAndJoinable()
    {
        var harness = new Harness(services => services.AddEventEmitter<SampleFeed>());
        var lastConnectedAt = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        var feed = Assert.Single(harness.Registry.Feeds);
        Assert.Equal("sample", feed.Key);
        Assert.Same(harness.Services.GetRequiredService<SampleFeed>(), feed.Value);

        Assert.True(await feed.Value.ConnectAsync("connection", User(2), lastConnectedAt));
        Assert.True(feed.Value.IsListening("connection"));
        Assert.Equal(lastConnectedAt, harness.Services.GetRequiredService<SampleFeed>().LastConnectedAt);
        harness.Groups.Verify(groups => groups.AddToGroupAsync("connection", "sample", It.IsAny<CancellationToken>()), Times.Once);

        await feed.Value.SendAsync("ping", 1);
        await feed.Value.SendToUserAsync(User(2), "pong", 2);
        await feed.Value.SendToUserAsync(User(3), "pong", 3);
        (string, string, int)[] sent = [("connection", "sample:connected", 2), ("group:sample", "sample:ping", 1), ("connection", "sample:pong", 2)];
        Assert.Equal(sent, harness.Sent.Select(entry => (entry.Target, entry.Method, (int)entry.Args.Single()!)));

        Assert.True(await feed.Value.DisconnectAsync("connection"));
        Assert.False(feed.Value.IsListening("connection"));
        harness.Groups.Verify(groups => groups.RemoveFromGroupAsync("connection", "sample", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PluginFeed_CanConnect_RefusesAndSendsNothing()
    {
        var harness = new Harness(services => services.AddEventEmitter<AdminFeed>());
        var feed = harness.Registry.Feeds["sample.admin"];

        Assert.False(await feed.ConnectAsync("user-connection", User(2)));
        Assert.False(feed.IsListening("user-connection"));
        Assert.True(await feed.ConnectAsync("admin-connection", User(1, isAdmin: true)));

        harness.Groups.Verify(groups => groups.AddToGroupAsync("user-connection", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Groups.Verify(groups => groups.AddToGroupAsync("admin-connection", "sample.admin", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(harness.Sent);
    }

    [Fact]
    public void FeedNames_MatchIgnoringCase()
    {
        var harness = new Harness(services => services.AddEventEmitter<SampleFeed>());
        var name = Assert.Single(harness.Registry.Feeds).Key;

        Assert.True(harness.Registry.Feeds.ContainsKey(name.ToUpperInvariant()));
    }

    [Fact]
    public void PluginFeeds_SharingAName_KeepTheFirstAndWarn()
    {
        var harness = new Harness(services =>
        {
            services.AddEventEmitter<SampleFeed>();
            services.AddEventEmitter<OtherSampleFeed>();
        });

        Assert.IsType<SampleFeed>(Assert.Single(harness.Registry.Feeds).Value);
        Assert.Equal(LogLevel.Warning, Assert.Single(harness.Logger.Levels));
    }

    [Fact]
    public async Task PluginFeed_BeforeItIsAttached_SendsNothing()
    {
        var feed = new SampleFeed();

        await feed.SendAsync("ping", 1);
        await feed.SendToUserAsync(User(2), "pong", 2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => feed.ConnectAsync("connection", User(2)));
    }

    #endregion
}
