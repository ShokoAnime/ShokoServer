using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   A release search goes on past a provider suspended by its own failure
///   when another provider is left to ask, and fails otherwise.
/// </summary>
public class ReleaseSearchSkipTests
{
    [Theory]
    [InlineData(0, 2, false, true)]
    [InlineData(1, 2, false, false)]
    [InlineData(0, 2, true, false)]
    [InlineData(0, 1, false, false)]
    public void AFailedProvider_IsSkipped_OnlyWhenSuspendedAndAnotherIsLeft(int index, int count, bool isStillUsable, bool skipped)
        => Assert.Equal(skipped, VideoReleaseService.ShouldSkipFailedProvider(index, count, isStillUsable));

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(0, 1)]
    public void AProviderWhoseSourceIsDown_IsAlwaysPassedBy(int index, int count)
        => Assert.Equal(
            VideoReleaseService.FailedProviderOutcome.PassUnavailable,
            VideoReleaseService.DecideOnFailedProvider(new MetadataProviderUnavailableException(MetadataSource.TMDB), index, count, isStillUsable: true)
        );

    [Fact]
    public void AnyOtherFailure_KeepsItsOutcome()
    {
        Assert.Equal(VideoReleaseService.FailedProviderOutcome.Skip, VideoReleaseService.DecideOnFailedProvider(new InvalidOperationException(), 0, 2, isStillUsable: false));
        Assert.Equal(VideoReleaseService.FailedProviderOutcome.Fail, VideoReleaseService.DecideOnFailedProvider(new InvalidOperationException(), 0, 2, isStillUsable: true));
    }
}
