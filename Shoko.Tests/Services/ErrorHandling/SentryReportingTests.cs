using System;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services.ErrorHandling;

/// <summary>
///   Where and how the server reports to Sentry: to the admin's DSN, with the
///   admin's sample rate and plugins, on any build; nowhere for any other
///   non-empty value; otherwise to the built-in DSN with the built-in values,
///   never from a debug build.
/// </summary>
public class SentryReportingTests
{
    private const string Custom = "https://key@sentry.example.com/2";

    private static readonly Guid ThirdParty = Guid.Parse("0f8a1c2e-1111-2222-3333-444455556666");

    private static VersionInformation Build(ReleaseChannel channel)
        => new()
        {
            Version = new(6, 0, 0),
            RuntimeIdentifier = "linux-x64",
            AbstractionVersion = new(6, 0, 0),
            SourceRevision = null,
            ReleaseTag = null,
            Channel = channel,
            ReleasedAt = DateTime.UtcNow,
        };

    [Theory]
    [InlineData("false")]
    [InlineData("off")]
    [InlineData("sentry.example.com/2")]
    public void ADsnThatIsNotAUrl_TurnsReportingOff(string dsn)
        => Assert.Null(SentryInit.GetReporting(new() { Dsn = dsn }, Build(ReleaseChannel.Stable), false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptyDsn_KeepsTheBuiltInOne_AndItsValues(string? dsn)
    {
        var settings = new SentrySettings { Dsn = dsn, SampleRate = 1, ReportedPlugins = [ThirdParty] };

        Assert.Null(SentryInit.GetReporting(settings, Build(ReleaseChannel.Debug), false));
        // A build from source has no built-in DSN, so a stable one reports nowhere either.
        Assert.Equal(SentryInit.IsUsableDsn(Shoko.Server.Server.Constants.SentryDsn), SentryInit.GetReporting(settings, Build(ReleaseChannel.Stable), false) is { SampleRate: SentryInit.BuiltInSampleRate });
    }

    [Fact]
    public void TheLegacyOptOut_TurnsReportingOff_EvenWithADsnSet()
        => Assert.Null(SentryInit.GetReporting(new() { Dsn = Custom }, Build(ReleaseChannel.Stable), optedOutByEnvironment: true));

    [Theory]
    [InlineData(ReleaseChannel.Debug)]
    [InlineData(ReleaseChannel.Dev)]
    public void TheAdminsDsn_IsUsedOnAnyBuild_WithTheAdminsValues(ReleaseChannel channel)
    {
        var reporting = SentryInit.GetReporting(new() { Dsn = $" {Custom} ", SampleRate = 1, ReportedPlugins = [ThirdParty] }, Build(channel), false);

        Assert.NotNull(reporting);
        Assert.Equal((Custom, 1.0), (reporting.Dsn, reporting.SampleRate));
        Assert.Equal([ThirdParty], reporting.ReportedPlugins);
    }

    [Fact]
    public void ADebugBuildWithoutADsn_ReportsNowhere()
        => Assert.Null(SentryInit.GetReporting(new() { SampleRate = 1, ReportedPlugins = [ThirdParty] }, Build(ReleaseChannel.Debug), false));

    [Fact]
    public void TheDefaultPlugins_AreTheFirstPartyOnes()
        => Assert.Equal(SentryInit.FirstPartyPluginIDs.Order(), new SentrySettings().ReportedPlugins.Order());
}
