using System;
using Shoko.Server.Services.ErrorHandling;
using Xunit;

namespace Shoko.Tests.Services.ErrorHandling;

/// <summary>
///   Which failures reach Sentry by where they were thrown: the server, its
///   dependencies and first-party plugins are reported, third-party plugins
///   are not, and the runtime's own frames never decide.
/// </summary>
public class SentryPluginFilterTests
{
    private static readonly Guid ThirdParty = Guid.Parse("0f8a1c2e-1111-2222-3333-444455556666");

    private static readonly Guid Tmdb = Guid.Parse("85d0c34f-240f-4da3-8512-d0b45118a9f4");

    [Fact]
    public void AThirdPartyPluginThrowingThroughTheRuntime_IsNotReported()
        => Assert.False(SentryInit.DecideFromFrames([("System.Linq", null), ("CulturedRenamer", ThirdParty), ("Shoko.Server", null)]));

    [Fact]
    public void AFirstPartyPluginsOwnDependency_IsReported()
        => Assert.True(SentryInit.DecideFromFrames([("System.Net.Http", null), ("TMDbLib", Tmdb), ("Shoko.Plugin.Tmdb", Tmdb)]));

    [Fact]
    public void TheServerCallingAThirdPartyPlugin_IsReported()
        => Assert.True(SentryInit.DecideFromFrames([("System.Private.CoreLib", null), ("Shoko.Server", null), ("ShokoRelay", ThirdParty)]));

    [Fact]
    public void AServerDependency_IsReportedUntilAPluginFrameDecides()
    {
        Assert.True(SentryInit.DecideFromFrames([("NHibernate", null), ("Shoko.Server", null)]));
        Assert.False(SentryInit.DecideFromFrames([("Newtonsoft.Json", null), ("ShokoRelay", ThirdParty)]));
        Assert.True(SentryInit.DecideFromFrames([("NHibernate", null)]));
    }

    [Fact]
    public void OnlyRuntimeFrames_DecideNothing()
        => Assert.Null(SentryInit.DecideFromFrames([("System.Private.CoreLib", null), ("Microsoft.AspNetCore.Mvc.Core", null)]));

    [Fact]
    public void AFailureThrownByTheServersOwnCode_IsReported()
    {
        Exception thrown;
        try
        {
            throw new InvalidOperationException("from the tests");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.True(SentryInit.ThrownByReportedCode(new AggregateException(thrown)));
    }
}
