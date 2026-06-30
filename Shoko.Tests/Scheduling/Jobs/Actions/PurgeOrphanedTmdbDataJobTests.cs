using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Actions;

public class PurgeOrphanedTmdbDataJobTests
{
    private static PurgeOrphanedTmdbDataJob MakeJob(
        int thresholdDays,
        out Mock<ITmdbMetadataService> serviceMock)
    {
        var settings = new ServerSettings { TMDB = new TMDBSettings { AutoPurgeUnlinkedAfterDays = thresholdDays } };
        var settingsMock = new Mock<ISettingsProvider>();
        settingsMock.Setup(p => p.GetSettings()).Returns(settings);

        serviceMock = new Mock<ITmdbMetadataService>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ILoggerFactory))).Returns(NullLoggerFactory.Instance);

        var job = new PurgeOrphanedTmdbDataJob(settingsMock.Object, serviceMock.Object);
        job.Setup(serviceProviderMock.Object);
        return job;
    }

    [Fact]
    public async Task Execute_ThresholdZero_NeitherServiceMethodCalled()
    {
        var job = MakeJob(0, out var service);

        await job.Execute();

        service.Verify(s => s.PurgeAllUnusedShows(It.IsAny<DateTime?>()), Times.Never);
        service.Verify(s => s.PurgeAllUnusedMovies(It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ThresholdPositive_BothServiceMethodsCalledWithCorrectCutoff()
    {
        var before = DateTime.Now.AddDays(-14);
        var job = MakeJob(14, out var service);

        await job.Execute();

        service.Verify(s => s.PurgeAllUnusedShows(It.Is<DateTime?>(d => d.HasValue && d.Value >= before && d.Value <= DateTime.Now)), Times.Once);
        service.Verify(s => s.PurgeAllUnusedMovies(It.Is<DateTime?>(d => d.HasValue && d.Value >= before && d.Value <= DateTime.Now)), Times.Once);
    }
}
