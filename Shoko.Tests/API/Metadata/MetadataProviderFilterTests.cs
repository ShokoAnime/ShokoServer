using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.API.Metadata.FakeMetadataEntries;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Covers the refusals of the generic metadata routes: a provider out of
/// reach answers 502 and a suspended source 503, both with a <c>Retry-After</c>,
/// and a provider not configured 503 without one, each with a problem body.
/// </summary>
public class MetadataProviderFilterTests
{
    #region Fixture

    private static HttpContext Context(SuspensionStatus? status = null)
    {
        var suspensions = SuspensionTestDoubles.Service();
        if (status is not null)
            suspensions.Setup(s => s.GetForSource(It.IsAny<MetadataSource>())).Returns([status]);
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddSingleton(suspensions.Object).BuildServiceProvider(),
        };
    }

    private static ExceptionContext Failed(Exception exception, SuspensionStatus? status = null)
        => new(new ActionContext(Context(status), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = exception,
        };

    private static SuspensionStatus PausedFor(TimeSpan left)
        => SuspensionTestDoubles.Status(DateTime.UtcNow + left);

    private static SourceSuspension Suspended(DateTime? resumesAt = null, string? reason = null)
        => SourceSuspension.From([SuspensionTestDoubles.Status(resumesAt, reason)]);

    #endregion

    #region Unavailable

    [Fact]
    public void AProviderOutOfReachAnswers502WithTheWaitItNames()
    {
        var context = Failed(new MetadataProviderUnavailableException(Source, "Down.", TimeSpan.FromSeconds(42)));

        new MetadataProviderUnavailableAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(502, result.StatusCode);
        Assert.Equal("Down.", Assert.IsType<ProblemDetails>(result.Value).Detail);
        Assert.Equal("42", context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void AProviderOutOfReachWaitsOutThePauseOrAMinute()
    {
        var paused = Failed(new MetadataProviderUnavailableException(Source), PausedFor(TimeSpan.FromSeconds(20)));
        var unknown = Failed(new MetadataProviderUnavailableException(Source));

        new MetadataProviderUnavailableAttribute().OnException(paused);
        new MetadataProviderUnavailableAttribute().OnException(unknown);

        Assert.InRange(int.Parse(paused.HttpContext.Response.Headers.RetryAfter.ToString()), 19, 20);
        Assert.Equal("60", unknown.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void AnUnconfiguredProviderBehindABlockingWaitAnswers503()
    {
        var context = Failed(new AggregateException(new MetadataProviderNotConfiguredException(Source, "No API key is configured.")));

        new MetadataProviderUnavailableAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.Equal(503, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        Assert.Empty(context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void AFailureAnotherFilterHandledIsLeftAlone()
    {
        var context = Failed(new MetadataProviderUnavailableException(Source, "Down."));
        var handled = new StatusCodeResult(418);
        context.Result = handled;
        context.ExceptionHandled = true;

        new MetadataProviderUnavailableAttribute().OnException(context);

        Assert.Same(handled, context.Result);
        Assert.Empty(context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void OtherFailuresAreLeftAlone()
    {
        var context = Failed(new InvalidOperationException("Rejected."));

        new MetadataProviderUnavailableAttribute().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    #endregion

    #region Refusals

    [Fact]
    public void APluginProviderNotConfiguredAnswers503WithoutAWait()
    {
        var context = Failed(new MetadataProviderNotConfiguredException(Source, "No API key is set."));

        new MetadataProviderUnavailableAttribute().OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("No API key is set.", problem.Detail);
        Assert.Empty(context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void APausedSourceAnswers503WithAProblemAndAWait()
    {
        var response = new DefaultHttpContext().Response;

        var result = MetadataPauseResponses.Refuse(response, Source, Provider(configured: true), Suspended(DateTime.UtcNow.AddSeconds(15), "Rate limited."));

        Assert.NotNull(result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("Rate limited.", problem.Detail);
        Assert.InRange(int.Parse(response.Headers.RetryAfter.ToString()), 14, 15);
    }

    [Fact]
    public void APauseWithNoEndStillAsksForAWait()
    {
        var response = new DefaultHttpContext().Response;

        var result = MetadataPauseResponses.Refuse(response, Source, null, Suspended());

        Assert.Equal(503, result!.StatusCode);
        Assert.Equal(MetadataPauseResponses.DefaultRetryAfterSeconds.ToString(), response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void AProviderNotConfiguredIsRefusedBeforeAPauseAndNamed()
    {
        var response = new DefaultHttpContext().Response;
        var provider = Provider(configured: false, reason: "No API key is set.");

        var result = MetadataPauseResponses.Refuse(response, Source, provider, Suspended(reason: "No API key."));

        Assert.NotNull(result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("No API key is set.", problem.Detail);
        Assert.Equal(provider.ID, problem.Extensions["providerID"]);
        Assert.Equal(Source.Value, problem.Extensions["source"]);
        Assert.Empty(response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void ARunningConfiguredProviderIsNotRefused()
        => Assert.Null(MetadataPauseResponses.Refuse(new DefaultHttpContext().Response, Source, Provider(configured: true), SourceSuspension.None));

    private static MetadataProviderInfo Provider(bool configured, string? reason = null)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = "Fake",
            Description = string.Empty,
            Provider = Mock.Of<IMetadataProvider>(provider => provider.IsConfigured == configured && provider.NotConfiguredReason == reason),
            ConfigurationInfo = null,
            PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(MetadataProviderFilterTests), Guid.NewGuid()),
            SupportsSeries = true,
            SupportsMovies = false,
            SupportsCollections = false,
            SupportsAutoLinking = true,
            SupportsLookup = true,
            Source = Source,
            AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
            EnabledEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
            IsAutoLinker = true,
        };

    #endregion
}
