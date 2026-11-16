using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.SignalR;
using Shoko.Server.API.SignalR.Models;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   Covers the startup feed: the backlog then each new message once and in
///   order, the outcome, closing after a start and staying open after a
///   failure, and the hub gated by its policy.
/// </summary>
public class StartupEmitterTests
{
    private sealed class StartingServer
    {
        private readonly List<StartupMessageEntry> _messages = [];

        private readonly Mock<ISystemService> _mock = new();

        private DateTime _clock = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        public StartingServer()
        {
            _mock.SetupGet(s => s.IsStarted).Returns(() => IsStarted);
            _mock.SetupGet(s => s.InSetupMode).Returns(() => InSetupMode);
            _mock.SetupGet(s => s.StartupFailedException).Returns(() => Failure);
            _mock.SetupGet(s => s.StartupMessage).Returns(() => { lock (_messages) return _messages.LastOrDefault()?.Message ?? string.Empty; });
            _mock.SetupGet(s => s.StartupMessages).Returns(() => { lock (_messages) return _messages.ToArray(); });
            _mock.SetupGet(s => s.HasSetupToken).Returns(() => HasSetupToken);
        }

        public ISystemService Service => _mock.Object;

        public bool IsStarted { get; set; }

        public bool InSetupMode { get; set; }

        public StartupFailedException? Failure { get; set; }

        public bool HasSetupToken { get; set; }

        // Records a message in the list, as the server's setter does, without raising its event.
        public StartupMessageEntry Record(string message)
        {
            lock (_messages)
            {
                _clock = _clock.AddSeconds(1);
                var entry = new StartupMessageEntry { Message = message, Time = _clock };
                _messages.Add(entry);
                return entry;
            }
        }

        public void Raise(StartupMessageEntry entry)
            => _mock.Raise(s => s.StartupMessageChanged += null, Service, new StartupMessageChangedEventArgs { Entry = entry });

        public StartupMessageEntry Say(string message)
        {
            var entry = Record(message);
            Raise(entry);
            return entry;
        }

        public void Start()
        {
            IsStarted = true;
            _mock.Raise(s => s.Started += null, Service, EventArgs.Empty);
        }

        public void Fail(string message)
        {
            Failure = new(message);
            _mock.Raise(s => s.StartupFailed += null, Service, new StartupFailedEventArgs(Failure));
        }

        public void RequireSetup()
        {
            InSetupMode = true;
            _mock.Raise(s => s.SetupRequired += null, Service, EventArgs.Empty);
        }

        public void CompleteSetup()
        {
            InSetupMode = false;
            _mock.Raise(s => s.SetupCompleted += null, Service, EventArgs.Empty);
        }
    }

    private sealed class Recorder
    {
        public ConcurrentQueue<string> Sent { get; } = new();

        public ISingleClientProxy Proxy { get; }

        public Recorder()
        {
            var proxy = new Mock<ISingleClientProxy>();
            proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string method, object?[] args, CancellationToken _) => Sent.Enqueue(Describe(method, args[0])))
                .Returns(Task.CompletedTask);
            Proxy = proxy.Object;
        }

        private static string Describe(string method, object? payload)
            => payload switch
            {
                IEnumerable<StartupMessageSignalRModel> entries => $"{method.ToLowerInvariant()}:" + string.Join(",", entries.Select(entry => entry.Message)),
                StartupMessageSignalRModel entry => $"{method.ToLowerInvariant()}:{entry.Message}",
                StartupStateSignalRModel state => $"{method.ToLowerInvariant()}:{state.State}:{state.Message}",
                _ => method,
            };
    }

    private sealed class Connection(StartupEmitter emitter, string id, bool signedIn = false)
    {
        public Recorder Client { get; } = new();

        public bool Aborted { get; private set; }

        public Task Join() => emitter.JoinAsync(id, () => Aborted = true, signedIn);
    }

    private sealed class Feed
    {
        private readonly ConcurrentDictionary<string, Recorder> _clients = new();

        public Feed(StartingServer server)
        {
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Client(It.IsAny<string>())).Returns((string id) => _clients[id].Proxy);
            var hub = new Mock<IHubContext<StartupHub>>();
            hub.SetupGet(h => h.Clients).Returns(clients.Object);
            Instance = new(server.Service, hub.Object, NullLogger<StartupEmitter>.Instance, TimeSpan.Zero);
        }

        public StartupEmitter Instance { get; }

        public Connection Connect(string id, bool signedIn = false)
        {
            var connection = new Connection(Instance, id, signedIn);
            _clients[id] = connection.Client;
            return connection;
        }
    }

    [Fact]
    public async Task ItSendsTheBacklogThenEachNewMessageOnce()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var first = server.Say("Initializing database…");
        var second = server.Say("Loading plugins…");

        var connection = feed.Connect("a");
        await connection.Join();
        // An event for an entry the backlog carried arrives late.
        server.Raise(second);
        server.Raise(first);
        server.Say("Starting the queue…");
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:Initializing database…,Loading plugins…", "state:Starting:Loading plugins…", "message:Starting the queue…"], connection.Client.Sent);
    }

    [Fact]
    public async Task EventsOutOfOrderAreSentInTheListsOrder()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var connection = feed.Connect("a");
        await connection.Join();

        var first = server.Record("One");
        var second = server.Record("Two");
        server.Raise(second);
        server.Raise(first);
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:", "message:One", "message:Two"], connection.Client.Sent);
    }

    [Fact]
    public async Task EachConnectionGetsItsOwnBacklog()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var early = feed.Connect("early");
        await early.Join();
        server.Say("One");
        await feed.Instance.IdleAsync();

        var late = feed.Connect("late");
        await late.Join();
        server.Say("Two");
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:", "message:One", "message:Two"], early.Client.Sent);
        Assert.Equal(["backlog:One", "state:Starting:One", "message:Two"], late.Client.Sent);
    }

    [Fact]
    public async Task AStartSendsTheLastMessagesAndStartedThenCloses()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var connection = feed.Connect("a");
        await connection.Join();

        // The last message, whose event may come after Started.
        var complete = server.Record("Complete!");
        server.Start();
        server.Raise(complete);
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:", "message:Complete!", "state:Started:"], connection.Client.Sent);
        Assert.True(connection.Aborted);
        Assert.Equal(0, feed.Instance.Count);
    }

    [Fact]
    public async Task AConnectionThatSlipsInAfterTheStartIsToldAndClosed()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        server.Say("Complete!");
        server.IsStarted = true;

        var connection = feed.Connect("a");
        await connection.Join();
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:Complete!", "state:Started:"], connection.Client.Sent);
        Assert.True(connection.Aborted);
    }

    [Fact]
    public async Task AFailedStartSendsTheFailureAndStaysOpen()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var connection = feed.Connect("a");
        await connection.Join();

        server.Say("Initializing database…");
        server.Fail("The database could not be reached.");
        await feed.Instance.IdleAsync();
        var later = feed.Connect("b");
        await later.Join();
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:", "message:Initializing database…", "state:Failed:The database could not be reached."], connection.Client.Sent);
        Assert.Equal(["backlog:Initializing database…", "state:Failed:The database could not be reached."], later.Client.Sent);
        Assert.False(connection.Aborted);
        Assert.Equal(2, feed.Instance.Count);
    }

    [Fact]
    public async Task WithASetupTokenAFailedStartClosesWhoeverJoinedWithoutIt()
    {
        var server = new StartingServer { HasSetupToken = true };
        var feed = new Feed(server);
        var anonymous = feed.Connect("anonymous");
        await anonymous.Join();
        var signedIn = feed.Connect("signed-in", signedIn: true);
        await signedIn.Join();

        server.Fail("The database could not be reached.");
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:"], anonymous.Client.Sent);
        Assert.True(anonymous.Aborted);
        Assert.Equal(["backlog:", "state:Starting:", "state:Failed:The database could not be reached."], signedIn.Client.Sent);
        Assert.False(signedIn.Aborted);
        Assert.Equal(1, feed.Instance.Count);
    }

    [Fact]
    public async Task SetupModeIsAStateTheFeedStaysOpenThrough()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var connection = feed.Connect("a");
        await connection.Join();

        server.RequireSetup();
        await feed.Instance.IdleAsync();
        server.CompleteSetup();
        await feed.Instance.IdleAsync();
        server.Say("Initializing database…");
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:", "state:Waiting:", "state:Starting:", "message:Initializing database…"], connection.Client.Sent);
        Assert.False(connection.Aborted);
    }

    [Fact]
    public async Task ALeftConnectionIsSentNothingMore()
    {
        var server = new StartingServer();
        var feed = new Feed(server);
        var connection = feed.Connect("a");
        await connection.Join();

        feed.Instance.Leave("a");
        server.Say("One");
        await feed.Instance.IdleAsync();

        Assert.Equal(["backlog:", "state:Starting:"], connection.Client.Sent);
    }

    [Fact]
    public void TheHubIsExemptFromTheNotRunningCheckAndGatedByThePolicy()
    {
        var hub = typeof(StartupHub);

        Assert.NotNull(hub.GetCustomAttribute<InitFriendlyAttribute>());
        Assert.Equal("startup", hub.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Null(hub.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
