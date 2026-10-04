using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Covers how <see cref="QueueProcessorSettings.DefaultPoolMaxWorkers"/> becomes the Default
/// pool's worker cap.
/// </summary>
public class QueueProcessorSettingsTests
{
    private const int MaxTotalWorkers = 8;

    [Fact]
    public void DefaultPoolMaxWorkers_Unset_MatchesMaxTotalWorkers()
    {
        var settings = new QueueProcessorSettings();

        Assert.Equal(settings.GetEffectiveMaxTotalWorkers(), settings.GetEffectiveDefaultPoolMaxWorkers());
    }

    [Theory]
    [InlineData(0, MaxTotalWorkers)]
    [InlineData(MaxTotalWorkers + 1, MaxTotalWorkers)]
    [InlineData(MaxTotalWorkers - 1, MaxTotalWorkers - 1)]
    public void DefaultPoolMaxWorkers_IsCappedToMaxTotalWorkers(int defaultPoolMaxWorkers, int expected)
    {
        var settings = new QueueProcessorSettings { MaxTotalWorkers = MaxTotalWorkers, DefaultPoolMaxWorkers = defaultPoolMaxWorkers };

        Assert.Equal(expected, settings.GetEffectiveDefaultPoolMaxWorkers());
    }
}
