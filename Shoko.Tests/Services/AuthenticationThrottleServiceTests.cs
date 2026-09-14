using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.User;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

public class AuthenticationThrottleServiceTests
{
    [Fact]
    public void User_IsNotLockedOutBeforeTheAttemptLimit()
    {
        var (throttler, _, _) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts - 1; attempt++)
            throttler.RegisterFailure(user);

        Assert.Null(throttler.GetRemainingLockout(user));
    }

    [Fact]
    public void User_IsLockedOutForTheInitialLockoutAtTheAttemptLimit()
    {
        var (throttler, _, _) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(user);

        var remaining = throttler.GetRemainingLockout(user);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, throttler.InitialLockout - TimeSpan.FromMinutes(1), throttler.InitialLockout);
    }

    [Fact]
    public void User_LockoutEscalatesWithEveryFurtherAttempt()
    {
        var (throttler, _, _) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts + 1; attempt++)
            throttler.RegisterFailure(user);

        var remaining = throttler.GetRemainingLockout(user);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, throttler.InitialLockout, throttler.InitialLockout * 2);
    }

    [Fact]
    public void AnAttemptLimitOfOne_LocksOutOnTheFirstFailure()
    {
        var (throttler, settings, save) = CreateThrottler();
        var user = CreateUser("alice");
        settings.Web.AuthenticationThrottle.MaxFailedAttempts = 1;
        save();

        throttler.RegisterFailure(user);

        var remaining = throttler.GetRemainingLockout(user);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, throttler.InitialLockout - TimeSpan.FromMinutes(1), throttler.InitialLockout);
    }

    [Fact]
    public void Reset_ClearsTheLockout()
    {
        var (throttler, _, _) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(user);
        throttler.Reset(user);

        Assert.Null(throttler.GetRemainingLockout(user));
    }

    [Fact]
    public void ClientAndUserLockouts_AreTrackedSeparately()
    {
        var (throttler, _, _) = CreateThrottler();
        var context = CreateContext("10.0.0.5");
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(context);

        Assert.NotNull(throttler.GetRemainingLockout(context));
        Assert.Null(throttler.GetRemainingLockout(user));
    }

    [Fact]
    public void AUsernameShapedLikeAClientKey_DoesNotLockOutThatClient()
    {
        var (throttler, _, _) = CreateThrottler();
        var context = CreateContext("10.0.0.5");
        var user = CreateUser("ip:10.0.0.5");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(user);

        Assert.NotNull(throttler.GetRemainingLockout(user));
        Assert.Null(throttler.GetRemainingLockout(context));
    }

    [Fact]
    public void UnknownUsernames_AreThrottledToo()
    {
        var (throttler, _, _) = CreateThrottler();

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure("ghost");

        Assert.NotNull(throttler.GetRemainingLockout("ghost"));
    }

    [Fact]
    public void ThrottleAuthentication_AllowsAnAttemptWhenNothingIsLockedOut()
    {
        var (throttler, _, _) = CreateThrottler();
        var context = CreateContext("10.0.0.5");

        Assert.Null(throttler.ThrottleAuthentication(context, "alice"));
        Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public void ThrottleAuthentication_BlocksALockedOutUsernameEvenIfNoSuchUserExists()
    {
        var (throttler, _, _) = CreateThrottler();
        var context = CreateContext("10.0.0.5");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure("ghost");

        var result = throttler.ThrottleAuthentication(context, "ghost");

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        Assert.InRange(int.Parse(context.Response.Headers.RetryAfter.ToString()), 1, (int)throttler.InitialLockout.TotalSeconds);
    }

    [Fact]
    public void ThrottleAuthentication_BlocksALockedOutClientForAnyUsername()
    {
        var (throttler, _, _) = CreateThrottler();
        var context = CreateContext("10.0.0.5");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(context);

        Assert.NotNull(throttler.ThrottleAuthentication(context, "alice"));
        Assert.Null(throttler.ThrottleAuthentication(CreateContext("10.0.0.6"), "alice"));
    }

    [Fact]
    public void SavedSettings_ChangeTheExposedPolicy()
    {
        var (throttler, settings, save) = CreateThrottler();

        settings.Web.AuthenticationThrottle.MaxFailedAttempts = 3;
        settings.Web.AuthenticationThrottle.AttemptWindowMinutes = 5;
        settings.Web.AuthenticationThrottle.InitialLockoutMinutes = 2;
        settings.Web.AuthenticationThrottle.MaxLockoutMinutes = 60;
        save();

        Assert.Equal(3, throttler.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), throttler.AttemptWindow);
        Assert.Equal(TimeSpan.FromMinutes(2), throttler.InitialLockout);
        Assert.Equal(TimeSpan.FromMinutes(60), throttler.MaxLockout);
    }

    [Fact]
    public void LoweringTheMaxLockout_ShortensAnExistingLockout()
    {
        var (throttler, settings, save) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(user);

        settings.Web.AuthenticationThrottle.MaxLockoutMinutes = 15;
        settings.Web.AuthenticationThrottle.InitialLockoutMinutes = 1;
        save();

        var remaining = throttler.GetRemainingLockout(user);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void RaisingTheMaxLockout_DoesNotExtendAnExistingLockout()
    {
        var (throttler, settings, save) = CreateThrottler();
        var user = CreateUser("alice");

        for (var attempt = 0; attempt < throttler.MaxFailedAttempts; attempt++)
            throttler.RegisterFailure(user);

        settings.Web.AuthenticationThrottle.MaxLockoutMinutes = 10080;
        save();

        var remaining = throttler.GetRemainingLockout(user);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void Validation_RejectsAnInitialLockoutAboveTheMaxLockout()
    {
        var settings = new ServerSettings();
        settings.Web.AuthenticationThrottle.InitialLockoutMinutes = 60;
        settings.Web.AuthenticationThrottle.MaxLockoutMinutes = 30;

        var errors = ServerSettings.Validate(settings, null!, null!);

        Assert.True(errors.ContainsKey("Web.AuthenticationThrottle.InitialLockoutMinutes"));
    }

    [Fact]
    public void Validation_AcceptsTheDefaults()
    {
        var errors = ServerSettings.Validate(new ServerSettings(), null!, null!);

        Assert.False(errors.ContainsKey("Web.AuthenticationThrottle.InitialLockoutMinutes"));
    }

    private static (AuthenticationThrottleService Throttler, ServerSettings Settings, Action Save) CreateThrottler()
    {
        var settings = new ServerSettings();
        var mockService = new Mock<IConfigurationService>();
        mockService
            .Setup(s => s.GetConfigurationInfo<ServerSettings>())
            .Returns((ConfigurationInfo)null!);
        mockService
            .Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(settings);

        var provider = new ConfigurationProvider<ServerSettings>(mockService.Object);
        var throttler = new AuthenticationThrottleService(NullLogger<AuthenticationThrottleService>.Instance, provider);
        return (throttler, settings, () => mockService.Raise(s => s.Saved += null, mockService.Object, new ConfigurationSavedEventArgs { ConfigurationInfo = null! }));
    }

    private static IUser CreateUser(string username)
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Username).Returns(username);
        return user.Object;
    }

    private static HttpContext CreateContext(string ipAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);
        return context;
    }
}
