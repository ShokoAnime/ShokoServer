using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.SignalR;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Actors;

/// <summary>
///   Who the current flow runs for: set by the middleware and the hub filter, ended with the
///   request, and never kept by work that outlives it.
/// </summary>
public class ActorContextTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    internal static ApiToken Token(string device = "desktop", int userID = 1)
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.LocalID).Returns(userID);
        user.SetupGet(u => u.Username).Returns("user" + userID);
        return new ApiToken(user.Object, device, Guid.NewGuid().ToString(), null);
    }

    #region Scopes

    [Fact]
    public void BeginScope_SetsAndRestoresTheActor()
    {
        var context = new ActorContext();
        var outer = Token("outer");
        var inner = Token("inner");

        Assert.Null(context.Current);
        using (context.BeginScope(outer))
        {
            Assert.Same(outer, context.Current);
            using (context.BeginScope(inner))
                Assert.Same(inner, context.Current);
            using (context.BeginScope(null))
                Assert.Null(context.Current);
            Assert.Same(outer, context.Current);
        }

        Assert.Null(context.Current);
    }

    [Fact]
    public async Task TaskStartedInScope_SeesTheActorOnlyWhileTheScopeLasts()
    {
        var token = Token();
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<(ApiToken? During, ApiToken? After)> task;
        var scope = ActorContext.Begin(token);
        try
        {
            task = Task.Run(async () =>
            {
                var during = ActorContext.CurrentActor;
                read.SetResult();
                await release.Task;
                return (during, ActorContext.CurrentActor);
            }, TestContext.Current.CancellationToken);
            await read.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            scope.Dispose();
        }

        release.SetResult();
        var (during, after) = await task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.Same(token, during);
        Assert.Null(after);
    }

    [Fact]
    public void TimerStartedInScope_SeesNoActorOnceTheScopeEnds()
    {
        var ticks = new System.Collections.Concurrent.BlockingCollection<ApiToken?>();
        Timer timer;
        using (ActorContext.Begin(Token()))
            timer = new Timer(_ => ticks.Add(ActorContext.CurrentActor), null, TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);

        using (timer)
        {
            Assert.True(ticks.TryTake(out var seen, _timeout));
            Assert.Null(seen);
        }
    }

    [Fact]
    public async Task DetachedTimer_SeesNoActorEvenWhileTheScopeLasts()
    {
        var tick = new TaskCompletionSource<ApiToken?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ActorContext.Begin(Token()))
        {
            Timer timer;
            using (DetachedFlow.Suppress())
                timer = new Timer(_ => tick.TrySetResult(ActorContext.CurrentActor), null, 0, Timeout.Infinite);
            using (timer)
                Assert.Null(await tick.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Carry_KeepsTheActorAfterTheScopeEnds()
    {
        var token = Token();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new TaskCompletionSource<ApiToken?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ActorContext.Begin(token))
        {
            _ = Task.Run(ActorContext.Carry(async () =>
            {
                await release.Task;
                seen.TrySetResult(ActorContext.CurrentActor);
            }), TestContext.Current.CancellationToken);
        }

        release.SetResult();
        Assert.Same(token, await seen.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Middleware

    [Fact]
    public async Task Middleware_SetsTheRequestTokenAndClearsItAfterwards()
    {
        var token = Token();
        var users = new Mock<IUserService>();
        users.Setup(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>())).Returns(token);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("apikey", token.Token)], "ShokoServer")) };
        ApiToken? seen = null;
        var middleware = new ActorContextMiddleware(_ =>
        {
            seen = ActorContext.CurrentActor;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, new ActorContext(), users.Object);

        Assert.Same(token, seen);
        Assert.Null(ActorContext.CurrentActor);
    }

    [Fact]
    public async Task Middleware_LeavesAnAnonymousRequestToTheSystem()
    {
        var users = new Mock<IUserService>();
        users.Setup(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>())).Returns(Token());
        ApiToken? seen = Token("unset");
        var middleware = new ActorContextMiddleware(_ =>
        {
            seen = ActorContext.CurrentActor;
            return Task.CompletedTask;
        });

        using (ActorContext.Begin(Token("outer")))
            await middleware.Invoke(new DefaultHttpContext(), new ActorContext(), users.Object);

        Assert.Null(seen);
        users.Verify(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>()), Times.Never);
    }

    #endregion

    #region Hub filter

    [Fact]
    public async Task HubFilter_SetsTheConnectionTokenForTheCall()
    {
        var token = Token("browser");
        var users = new Mock<IUserService>();
        users.Setup(u => u.GetApiTokenFromHttpContext(It.IsAny<HttpContext>())).Returns(token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("apikey", token.Token)], "ShokoServer"));
        var http = new DefaultHttpContext { User = principal };
        http.Features.Set<IHttpContextFeature>(new HttpContextFeature { HttpContext = http });
        var caller = new Mock<HubCallerContext>();
        caller.SetupGet(c => c.User).Returns(principal);
        caller.SetupGet(c => c.Features).Returns(http.Features);
        var invocation = new HubInvocationContext(caller.Object, Mock.Of<IServiceProvider>(), Mock.Of<Hub>(), typeof(Hub).GetMethod(nameof(Hub.OnConnectedAsync))!, []);
        var filter = new ActorHubFilter(new ActorContext(), users.Object);
        ApiToken? seen = null;

        await filter.InvokeMethodAsync(invocation, _ =>
        {
            seen = ActorContext.CurrentActor;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.Same(token, seen);
        Assert.Null(ActorContext.CurrentActor);
    }

    private sealed class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    #endregion
}
