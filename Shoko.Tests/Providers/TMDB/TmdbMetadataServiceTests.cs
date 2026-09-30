using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Providers.TMDB;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers what <see cref="TmdbMetadataService"/>, a shim over the generic
/// refresh and purge services, does of its own: mapping its update options
/// onto a refresh and leaving a show without an ID alone.
/// </summary>
public class TmdbMetadataServiceTests
{
    #region Fixtures

    private static readonly MetadataGuid Show = new(MetadataSource.TMDB, MetadataEntityType.Series, "5");

    private static (TmdbMetadataService Service, Mock<IMetadataRefreshService> Refresh) Build()
    {
        var refresh = new Mock<IMetadataRefreshService>();
        return (new TmdbMetadataService(null!, refresh.Object, Mock.Of<IMetadataPurgeService>()), refresh);
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task AScheduledShowUpdateIsAQueuedRequestedRefresh()
    {
        var (service, refresh) = Build();

        await service.ScheduleUpdateOfShow(new TmdbShowUpdateOptions { ShowId = 5, ForceRefresh = true, DownloadImages = true, DownloadNetworks = false });

        refresh.Verify(r => r.RefreshEntry(
            Show,
            true,
            It.Is<MetadataRefreshOptions>(options =>
                options.DownloadImages &&
                options.DownloadNetworks == false &&
                options.Reason == MetadataRefreshReason.Requested),
            false,
            false,
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task AShowWithoutAnIDIsLeftAlone()
    {
        var (service, refresh) = Build();

        Assert.False(await service.UpdateShow(new TmdbShowUpdateOptions { ShowId = 0 }));
        await service.ScheduleUpdateOfShow(new TmdbShowUpdateOptions { ShowId = 0 });

        refresh.VerifyNoOtherCalls();
    }

    #endregion
}
