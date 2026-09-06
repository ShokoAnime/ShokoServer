using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
                AniDBTestDoubles.SocketHandlerFactory(Socket));
        }

        public bool Init() => Handler.Init(Username, Password, UnroutableHost, 9000, 4556);
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
    public void InitBuildsTheSocketAndRecordsWhetherItConnected()
    {
        var harness = new Harness();

        Assert.True(harness.Init());
        Assert.True(harness.Socket.ConnectionAttempted);
        Assert.True(harness.Handler.IsNetworkAvailable);
    }

    [Fact]
    public void InitRecordsAFailureToConnect()
    {
        var harness = new Harness(socketConnects: false);

        harness.Init();

        Assert.False(harness.Handler.IsNetworkAvailable);
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData(Username, null)]
    [InlineData(Username, "")]
    public void InitRefusesIncompleteCredentials(string? username, string? password)
    {
        var harness = new Harness();

        Assert.False(harness.Handler.Init(username, password, UnroutableHost, 9000, 4556));
        Assert.False(harness.Socket.ConnectionAttempted);
    }

    [Fact]
    public void SendingBeforeInitIsRefused()
    {
        var harness = new Harness();

        // No socket has been built, so there is nothing to send through.
        Assert.Throws<ObjectDisposedException>(() => harness.Handler.SendDirectly("PING"));
    }

    #endregion

    #region Sending and receiving

    [Fact]
    public void AReplyIsDecodedAndReturned()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(Reply("300 PONG"));

        Assert.Equal("300 PONG", harness.Handler.SendDirectly("PING", isPing: true));
    }

    [Fact]
    public void TheCommandIsSentAsUnicodeByDefault()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(Reply("300 PONG"));

        harness.Handler.SendDirectly("PING");

        var sent = Assert.Single(harness.Socket.Sent);
        Assert.Equal(new UnicodeEncoding(true, false).GetBytes("PING"), sent);
    }

    [Fact]
    public void TheCommandCanBeSentAsAscii()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(Reply("300 PONG"));

        harness.Handler.SendDirectly("PING", needsUnicode: false);

        Assert.Equal(Encoding.ASCII.GetBytes("PING"), Assert.Single(harness.Socket.Sent));
    }

    [Fact]
    public void AByteOrderMarkIsStrippedFromTheReply()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(UnicodeReply("300 PONG"));

        // The mark is a decoding artefact, not part of the response.
        Assert.Equal("300 PONG", harness.Handler.SendDirectly("PING"));
    }

    #endregion

    #region Ban handling

    [Fact]
    public void AnAllZeroReplyIsTreatedAsABan()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(new byte[16]);

        // A silent socket cannot be told apart from a ban, and assuming the worse is what stops the
        // server digging the hole deeper.
        var exception = Assert.Throws<AniDBBannedException>(() => harness.Handler.SendDirectly("PING"));

        Assert.Equal(UpdateType.UDPBan, exception.BanType);
        Assert.True(harness.Handler.IsBanned);
    }

    [Fact]
    public void TheUdpBanExpiryIsAnHourAndAHalfAfterItStarted()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(new byte[16]);

        var exception = Assert.Throws<AniDBBannedException>(() => harness.Handler.SendDirectly("PING"));

        Assert.Equal(1.5D, harness.Handler.BanTimerResetLength);
        Assert.Equal(harness.Handler.BanTime!.Value.AddHours(1.5D), exception.BanExpires);
    }

    [Fact]
    public void SendRefusesToTalkWhileBanned()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(new byte[16]);
        Assert.Throws<AniDBBannedException>(() => harness.Handler.SendDirectly("PING"));

        Assert.Throws<AniDBBannedException>(() => harness.Handler.Send("PING"));

        // Only the first call reached the socket; the ban check short-circuits the rest.
        Assert.Single(harness.Socket.Sent);
    }

    [Fact]
    public void ANonZeroReplyIsNotMistakenForABan()
    {
        var harness = new Harness();
        harness.Init();
        harness.Socket.Respond(Reply("500 LOGIN FAILED"));

        Assert.Equal("500 LOGIN FAILED", harness.Handler.SendDirectly("AUTH"));
        Assert.False(harness.Handler.IsBanned);
    }

    #endregion

    #region Connectivity

    [Fact]
    public void NothingIsSentWithoutInternet()
    {
        var harness = new Harness(availability: NetworkAvailability.NoInterfaces);
        harness.Init();

        Assert.ThrowsAny<Exception>(() => harness.Handler.SendDirectly("PING"));

        // The request is abandoned before it reaches the socket rather than timing out on it.
        Assert.Empty(harness.Socket.Sent);
    }

    [Fact]
    public void APartialInternetConnectionIsGoodEnoughToTry()
    {
        var harness = new Harness(availability: NetworkAvailability.PartialInternet);
        harness.Init();
        harness.Socket.Respond(Reply("300 PONG"));

        Assert.Equal("300 PONG", harness.Handler.SendDirectly("PING"));
    }

    #endregion
}
