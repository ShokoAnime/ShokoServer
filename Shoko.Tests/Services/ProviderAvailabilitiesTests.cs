using System;
using System.Runtime.CompilerServices;
using Moq;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// A provider is off when disabled, then unconfigured, then suspended, in that
/// order, and a suspension with no end leaves the resume time open.
/// </summary>
public class ProviderAvailabilitiesTests
{
    private static SuspensionStatus Suspended(string name, DateTime? resumesAt)
        => new()
        {
            Provider = new()
            {
                ID = Guid.NewGuid(),
                Provider = Mock.Of<ISuspensionProvider>(),
                PluginInfo = (LocalPluginInfo)RuntimeHelpers.GetUninitializedObject(typeof(LocalPluginInfo)),
                Name = name,
                HeldProviderTypes = [],
            },
            Suspensions = [],
            IsSuspended = true,
            ResumesAt = resumesAt,
        };

    [Fact]
    public void Disabled_WinsOverEverythingElse()
        => Assert.Equal(
            ProviderAvailabilityKind.Disabled,
            ProviderAvailabilities.Get("A", false, false, null, [Suspended("S", null)]).Kind
        );

    [Fact]
    public void NotConfigured_WinsOverSuspended_AndKeepsTheProvidersReason()
    {
        var availability = ProviderAvailabilities.Get("A", true, false, "No key.", [Suspended("S", null)]);

        Assert.Equal(ProviderAvailabilityKind.NotConfigured, availability.Kind);
        Assert.Equal("No key.", availability.Reason);
    }

    [Fact]
    public void Suspended_ResumesWhenTheLastSuspensionEnds()
    {
        var end = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        var availability = ProviderAvailabilities.Get("A", true, true, null, [Suspended("S", end.AddHours(-1)), Suspended("T", end)]);

        Assert.Equal(ProviderAvailabilityKind.Suspended, availability.Kind);
        Assert.Equal(end, availability.ResumesAt);
        Assert.Equal(2, availability.Suspensions.Count);
    }

    [Fact]
    public void Suspended_WithoutAnEnd_HasNoResumeTime()
        => Assert.Null(ProviderAvailabilities.Get("A", true, true, null, [Suspended("S", DateTime.UtcNow), Suspended("T", null)]).ResumesAt);

    [Fact]
    public void Available_WhenNothingStopsIt()
        => Assert.True(ProviderAvailabilities.Get("A", true, true, null, []).IsAvailable);
}
