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
}
