using Moq;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.Release;
using Xunit;

namespace Shoko.Tests.Video.Release;

public class ReleaseStatusTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ReleaseCopiesKeepCorruptionAndDeprecationSeparate(bool corrupted, bool deprecated)
    {
        var release = new ReleaseInfoWithProvider(new ReleaseInfo { IsCorrupted = corrupted, IsDeprecated = deprecated }, "AniDB");
        var stored = new StoredReleaseInfo(Mock.Of<IVideo>(), release);
        var api = new Shoko.Server.API.v3.Models.Release.ReleaseInfo(stored);
        var signalR = new Shoko.Server.API.SignalR.Models.ReleaseInfoSignalRModel(stored);

        Assert.Equal(corrupted, stored.IsCorrupted);
        Assert.Equal(deprecated, stored.IsDeprecated);
        Assert.Equal(corrupted, api.IsCorrupted);
        Assert.Equal(deprecated, api.IsDeprecated);
        Assert.Equal(corrupted, signalR.IsCorrupted);
        Assert.Equal(deprecated, signalR.IsDeprecated);
    }
}
