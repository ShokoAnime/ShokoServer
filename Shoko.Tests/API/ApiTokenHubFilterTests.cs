using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers closing hub connections when their API token is invalidated.
/// </summary>
public class ApiTokenHubFilterTests
{
    [Fact]
    public async Task InvalidatingATokenClosesOnlyItsConnections()
    {
        var users = new Mock<IUserService>();
        using var filter = new ApiTokenHubFilter(users.Object, NullLogger<ApiTokenHubFilter>.Instance);
        var revoked = Connection("revoked-key", "a");
        var kept = Connection("kept-key", "b");
        await filter.OnConnectedAsync(Lifetime(revoked), _ => Task.CompletedTask);
        await filter.OnConnectedAsync(Lifetime(kept), _ => Task.CompletedTask);

        users.Raise(u => u.ApiTokenInvalidated += null, Invalidated("revoked-key"));

        revoked.Verify(c => c.Abort(), Times.Once);
        kept.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public async Task ADisconnectedConnectionIsLeftAlone()
    {
        var users = new Mock<IUserService>();
        using var filter = new ApiTokenHubFilter(users.Object, NullLogger<ApiTokenHubFilter>.Instance);
        var connection = Connection("key", "a");
        await filter.OnConnectedAsync(Lifetime(connection), _ => Task.CompletedTask);
        await filter.OnDisconnectedAsync(Lifetime(connection), null, (_, _) => Task.CompletedTask);

        users.Raise(u => u.ApiTokenInvalidated += null, Invalidated("key"));

        connection.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public async Task ATokenInvalidatedWhileConnectingIsClosedAtOnce()
    {
        var users = new Mock<IUserService>();
        users.Setup(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>())).Returns((ApiToken?)null);
        using var filter = new ApiTokenHubFilter(users.Object, NullLogger<ApiTokenHubFilter>.Instance);
        var connection = Connection("key", "a", withHttpContext: true);

        await filter.OnConnectedAsync(Lifetime(connection), _ => Task.CompletedTask);

        connection.Verify(c => c.Abort(), Times.Once);
    }

    private static Mock<HubCallerContext> Connection(string key, string connectionID, bool withHttpContext = false)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("apikey", key)], "ShokoServer"));
        var http = new DefaultHttpContext { User = principal };
        if (withHttpContext)
            http.Features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = http });
        var connection = new Mock<HubCallerContext>();
        connection.SetupGet(c => c.User).Returns(principal);
        connection.SetupGet(c => c.ConnectionId).Returns(connectionID);
        connection.SetupGet(c => c.Features).Returns(http.Features);
        return connection;
    }

    private static HubLifetimeContext Lifetime(Mock<HubCallerContext> connection)
        => new(connection.Object, Mock.Of<IServiceProvider>(), Mock.Of<Hub>());

    private static ApiTokenChangedEventArgs Invalidated(string key)
        => new() { ApiToken = new ApiToken(Mock.Of<IUser>(), "device", key, null), OccurredAt = DateTime.UtcNow };

    private sealed class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}
