using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Authentication;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Actors;

/// <summary>
///   The events that have an <c>Actor</c> carry the one of the request that raised them.
/// </summary>
public class ActorEventStampingTests
{
    [Fact]
    public async Task LinkChangeFromARequest_CarriesTheRequestToken()
    {
        var token = ActorContextTests.Token();
        var tracker = new MetadataLinkChangeTracker();
        MetadataLinksChangedEventArgs? raised = null;
        tracker.Changed += (_, e) => raised = e;

        await RunRequest(token, () =>
        {
            using (tracker.Begin(MetadataLinkChangeReason.Other))
                tracker.Record([Row()]);
        });

        Assert.NotNull(raised);
        Assert.Same(token, raised.Actor);
    }

    [Fact]
    public async Task TokenGeneratedFromARequest_CarriesTheRequestToken()
    {
        var token = ActorContextTests.Token("admin-browser");
        var harness = new Services.UserServiceApiTokenEventTests.Harness();

        await RunRequest(token, () => harness.Service.GenerateApiTokenForUser(harness.Users[0], "new-device").GetAwaiter().GetResult());

        var generated = Assert.Single(harness.Generated);
        Assert.Same(token, generated.Actor);
        Assert.Equal("new-device", generated.ApiToken.Device);
    }

    private static MetadataLinkChangeTracker.LinkRowChange Row()
        => new(MetadataEntityType.Series, MetadataSource.TMDB, 1, null, null, null, MatchRating.UserVerified);

    /// <summary>
    ///   Runs <paramref name="work"/> as the action of a request authenticated with
    ///   <paramref name="token"/>, through the real middleware.
    /// </summary>
    private static Task RunRequest(ApiToken token, Action work)
    {
        var users = new Mock<IUserService>();
        users.Setup(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>())).Returns(token);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("apikey", token.Token)], "ShokoServer")) };
        var middleware = new ActorContextMiddleware(_ =>
        {
            work();
            return Task.CompletedTask;
        });
        return middleware.Invoke(context, new ActorContext(), users.Object);
    }
}
