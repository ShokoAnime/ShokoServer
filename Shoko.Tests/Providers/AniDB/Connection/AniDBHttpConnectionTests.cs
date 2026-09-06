using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.Connection;

/// <summary>
/// Covers <see cref="AniDBHttpConnectionHandler"/> — the ban detection and request gating around
/// every AniDB HTTP call.
/// </summary>
/// <remarks>
/// AniDB bans clients that misbehave, and a ban costs the user a day of metadata. The handler is
/// what is supposed to notice one and stop talking, so it is worth knowing it does. Every request
/// here is answered by a stub <see cref="HttpMessageHandler"/>; no connection is ever opened.
/// </remarks>
public class AniDBHttpConnectionTests
{
    private const string BannedBody = "<error>banned</error>";

    private static (AniDBHttpConnectionHandler Handler, AniDBTestDoubles.StubHttpMessageHandler Http) Create()
    {
        StubSettingsProvider.Install();
        // The transport below is a stub, so nothing is ever dialed; pointing the base address at an
        // RFC 2606 reserved host makes that plain rather than implicit.
        ISettingsProvider.Instance.GetSettings().AniDb.HTTPServerUrl = "http://anidb.invalid";
        var http = new AniDBTestDoubles.StubHttpMessageHandler();
        var handler = new AniDBHttpConnectionHandler(
            NullLoggerFactory.Instance,
            AniDBTestDoubles.HttpRateLimiter(),
            AniDBTestDoubles.HttpClientFactory(http));

        return (handler, http);
    }

    #region Successful calls

    [Fact]
    public async Task ASuccessfulCallReturnsTheBody()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, "<anime id=\"1\" />");

        var response = await handler.GetHttp("httpapi?request=anime&aid=1");

        Assert.Equal("<anime id=\"1\" />", response.Response);
        Assert.Equal(HttpStatusCode.OK, response.Code);
    }

    [Fact]
    public async Task TheRequestGoesToTheConfiguredServer()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, "<anime />");

        await handler.GetHttp("httpapi?request=anime&aid=1");

        var request = Assert.Single(http.Requests);
        Assert.Contains("httpapi?request=anime&aid=1", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task AnEmptyBodyIsNotMistakenForABan()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, string.Empty);

        var response = await handler.GetHttp("httpapi");

        Assert.Equal(string.Empty, response.Response);
        Assert.False(handler.IsBanned);
    }

    [Fact]
    public async Task AnOrdinaryBodyMentioningBannedElsewhereIsNotABan()
    {
        var (handler, http) = Create();
        // The marker is the element `>banned<`, not the word appearing in content.
        http.Respond(HttpStatusCode.OK, "<anime><title>The Banned Ones</title></anime>");

        await handler.GetHttp("httpapi");

        Assert.False(handler.IsBanned);
    }

    #endregion

    #region Ban handling

    [Fact]
    public async Task ABannedResponseThrows()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, BannedBody);

        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));
    }

    [Fact]
    public async Task ABannedResponseMarksTheConnectionBanned()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, BannedBody);

        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));

        Assert.True(handler.IsBanned);
        Assert.NotNull(handler.BanTime);
    }

    [Fact]
    public async Task TheBanMarkerIsMatchedRegardlessOfCase()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, "<error>BANNED</error>");

        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));
        Assert.True(handler.IsBanned);
    }

    [Fact]
    public async Task AFurtherCallWhileBannedNeverReachesTheNetwork()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, BannedBody);
        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));

        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));

        // Talking to AniDB while banned is what extends the ban, so the second call must not go out.
        Assert.Equal(1, http.CallCount);
    }

    [Fact]
    public async Task ABannedCallCanBeForcedThrough()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, BannedBody);
        await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));

        http.Respond(HttpStatusCode.OK, "<anime />");
        var response = await handler.GetHttp("httpapi", force: true);

        Assert.Equal("<anime />", response.Response);
        Assert.Equal(2, http.CallCount);
    }

    [Fact]
    public async Task TheBanExpiryIsTwelveHoursAfterItStarted()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.OK, BannedBody);

        var exception = await Assert.ThrowsAsync<AniDBBannedException>(() => handler.GetHttp("httpapi"));

        Assert.Equal(12, handler.BanTimerResetLength);
        Assert.Equal(handler.BanTime!.Value.AddHours(12), exception.BanExpires);
        Assert.Equal(UpdateType.HTTPBan, exception.BanType);
    }

    #endregion

    #region Failures

    [Fact]
    public async Task AServerErrorIsSurfacedRatherThanSwallowed()
    {
        var (handler, http) = Create();
        http.Respond(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.GetHttp("httpapi"));
        Assert.False(handler.IsBanned);
    }

    [Fact]
    public async Task ATransportFailureIsSurfaced()
    {
        var (handler, http) = Create();
        http.Throw(new HttpRequestException("no route to host"));

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.GetHttp("httpapi"));
    }

    [Fact]
    public async Task AFailedCallLeavesTheConnectionUsable()
    {
        var (handler, http) = Create();
        http.Throw(new HttpRequestException("transient"));
        await Assert.ThrowsAsync<HttpRequestException>(() => handler.GetHttp("httpapi"));

        http.Respond(HttpStatusCode.OK, "<anime />");
        var response = await handler.GetHttp("httpapi");

        Assert.Equal("<anime />", response.Response);
    }

    #endregion
}
