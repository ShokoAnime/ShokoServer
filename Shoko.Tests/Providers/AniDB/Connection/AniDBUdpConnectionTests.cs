using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.Connection;

/// <summary>
/// Covers <see cref="AniDBUDPConnectionHandler"/> — the encoding, ban detection and gating around
/// every AniDB UDP call.
/// </summary>
/// <remarks>
/// The socket is supplied through <see cref="IAniDBSocketHandlerFactory"/> and replaced here by a
/// stub that replays canned payloads, so nothing binds a port or sends a datagram. The interesting
/// behaviour is not the socket anyway: it is that an all-zero reply is treated as a ban, and that a
/// banned connection stops talking.
/// </remarks>
public class AniDBUdpConnectionTests
{
    /// <summary>
    /// Reserved by RFC 2606 and guaranteed never to resolve. The socket is a stub and never dials
    /// anything, but naming an unroutable host makes that impossible to get wrong by accident.
    /// </summary>
    private const string UnroutableHost = "anidb.invalid";

    private const string Username = "tester";
    private const string Password = "secret";

    private sealed class Harness
    {
        public AniDBUDPConnectionHandler Handler { get; }

        public AniDBTestDoubles.StubSocketHandler Socket { get; } = new();

        public ServerSettings Settings { get; } = new();

        public Harness(NetworkAvailability availability = NetworkAvailability.Internet, bool socketConnects = true)
        {
            Socket.IsConnected = socketConnects;

            var settingsProvider = new Mock<ISettingsProvider>();
            settingsProvider.Setup(p => p.GetSettings(It.IsAny<bool>())).Returns(Settings);

            var connectivity = new Mock<IConnectivityService>();
            connectivity.SetupGet(c => c.NetworkAvailability).Returns(availability);

            Handler = new AniDBUDPConnectionHandler(
                requestFactory: null!,
                NullLoggerFactory.Instance,
                settingsProvider.Object,
                AniDBTestDoubles.UdpRateLimiter(),
                connectivity.Object,
                new AniDbBanStateService(NullLogger<AniDbBanState>.Instance),
                AniDBTestDoubles.SocketHandlerFactory(Socket));
        }

        public Task<bool> Init(CancellationToken cancellationToken = default) =>
            Handler.InitAsync(Username, Password, UnroutableHost, 9000, 4556, cancellationToken);
    }

    /// <summary>
    /// A plain reply. Without a byte order mark the handler decodes as ASCII, which is what AniDB
    /// sends for ordinary status responses.
    /// </summary>
    private static byte[] Reply(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>A reply carrying a UTF-16 big-endian byte order mark, as used for text payloads.</summary>
    private static byte[] UnicodeReply(string text)
        => [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)];

    #region Initialisation

    [Fact]
    public async Task InitBuildsTheSocketAndRecordsWhetherItConnected()
    {
        var harness = new Harness();

        Assert.True(await harness.Init(TestContext.Current.CancellationToken));
        Assert.True(harness.Socket.ConnectionAttempted);
        Assert.True(harness.Handler.IsNetworkAvailable);
    }

    [Fact]
    public async Task InitRecordsAFailureToConnect()
    {
        var harness = new Harness(socketConnects: false);

        await harness.Init(TestContext.Current.CancellationToken);

        Assert.False(harness.Handler.IsNetworkAvailable);
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData(Username, null)]
    [InlineData(Username, "")]
    public async Task InitRefusesIncompleteCredentials(string? username, string? password)
    {
        var harness = new Harness();

        Assert.False(await harness.Handler.InitAsync(username, password, UnroutableHost, 9000, 4556, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(harness.Socket.ConnectionAttempted);
    }

    [Fact]
    public async Task SendingBeforeInitIsRefused()
    {
        var harness = new Harness();

        // No socket has been built, so there is nothing to send through.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion

    #region Sending and receiving

    [Fact]
    public async Task AReplyIsDecodedAndReturned()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(Reply("300 PONG"));

        Assert.Equal("300 PONG", await harness.Handler.SendDirectlyAsync("PING", isPing: true, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCommandIsSentAsUnicodeByDefault()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(Reply("300 PONG"));

        await harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken);

        var sent = Assert.Single(harness.Socket.Sent);
        Assert.Equal(new UnicodeEncoding(true, false).GetBytes("PING"), sent);
    }

    [Fact]
    public async Task TheCommandCanBeSentAsAscii()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(Reply("300 PONG"));

        await harness.Handler.SendDirectlyAsync("PING", needsUnicode: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.ASCII.GetBytes("PING"), Assert.Single(harness.Socket.Sent));
    }

    [Fact]
    public async Task AByteOrderMarkIsStrippedFromTheReply()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(UnicodeReply("300 PONG"));

        // The mark is a decoding artefact, not part of the response.
        Assert.Equal("300 PONG", await harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion

    #region Ban handling

    [Fact]
    public async Task AnAllZeroReplyIsTreatedAsABan()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(new byte[16]);

        // A silent socket cannot be told apart from a ban, and assuming the worse is what stops the
        // server digging the hole deeper.
        var exception = await Assert.ThrowsAsync<AniDBBannedException>(() => harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(UpdateType.UDPBan, exception.BanType);
        Assert.True(harness.Handler.IsBanned);
    }

    [Fact]
    public async Task TheUdpBanExpiryIsAnHourAndAHalfAfterItStarted()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(new byte[16]);

        var exception = await Assert.ThrowsAsync<AniDBBannedException>(() => harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1.5D, harness.Handler.BanTimerResetLength);
        Assert.Equal(harness.Handler.BanTime!.Value.AddHours(1.5D), exception.BanExpires);
    }

    [Fact]
    public async Task SendRefusesToTalkWhileBanned()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(new byte[16]);
        await Assert.ThrowsAsync<AniDBBannedException>(() => harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<AniDBBannedException>(() => harness.Handler.SendAsync("PING", cancellationToken: TestContext.Current.CancellationToken));

        // Only the first call reached the socket; the ban check short-circuits the rest.
        Assert.Single(harness.Socket.Sent);
    }

    [Fact]
    public async Task ANonZeroReplyIsNotMistakenForABan()
    {
        var harness = new Harness();
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(Reply("500 LOGIN FAILED"));

        Assert.Equal("500 LOGIN FAILED", await harness.Handler.SendDirectlyAsync("AUTH", cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(harness.Handler.IsBanned);
    }

    #endregion

    #region Connectivity

    [Fact]
    public async Task NothingIsSentWithoutInternet()
    {
        var harness = new Harness(availability: NetworkAvailability.NoInterfaces);
        await harness.Init(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<SocketException>(() => harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));

        // The request is abandoned before it reaches the socket rather than timing out on it.
        Assert.Empty(harness.Socket.Sent);
    }

    [Fact]
    public async Task APartialInternetConnectionIsGoodEnoughToTry()
    {
        var harness = new Harness(availability: NetworkAvailability.PartialInternet);
        await harness.Init(TestContext.Current.CancellationToken);
        harness.Socket.Respond(Reply("300 PONG"));

        Assert.Equal("300 PONG", await harness.Handler.SendDirectlyAsync("PING", cancellationToken: TestContext.Current.CancellationToken));
    }

    #endregion
}
