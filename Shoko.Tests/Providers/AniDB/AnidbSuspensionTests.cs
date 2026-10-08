using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Suspensions;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers how the AniDB connections' state turns into suspensions, and that
/// lifting a ban unbans.
/// </summary>
public class AnidbSuspensionTests
{
    [Fact]
    public async Task AUdpBanIsALiftableSuspensionAndARefusedLoginOneWithoutEnd()
    {
        var banStates = new AniDbBanStateService(NullLogger<AniDbBanState>.Instance);
        var plugins = new Mock<IPluginManager>();
        plugins.Setup(p => p.GetPluginInfo(typeof(AnidbUdpSuspensionProvider).Assembly))
            .Returns(PluginTestDoubles.CorePluginInfo(typeof(AnidbUdpSuspensionProvider), Guid.NewGuid()));
        using var service = new SuspensionService(plugins.Object, Mock.Of<IMetadataProviderManager>(), NullLogger<SuspensionService>.Instance);
        service.AddParts([new AnidbUdpSuspensionProvider(banStates), new AnidbHttpSuspensionProvider(banStates)]);
        var udp = new Mock<IUDPConnectionHandler>();
        udp.SetupGet(h => h.BanState).Returns(banStates.Udp);
        var http = new Mock<IHttpConnectionHandler>();
        http.SetupGet(h => h.BanState).Returns(banStates.Http);
        var monitor = new AnidbSuspensionMonitor(
            udp.Object,
            http.Object,
            new SuspensionReporter<AnidbUdpSuspensionProvider>(service),
            new SuspensionReporter<AnidbHttpSuspensionProvider>(service),
            NullLogger<AnidbSuspensionMonitor>.Instance
        );
        await monitor.StartAsync(TestContext.Current.CancellationToken);
        var udpStatus = () => service.GetAll().Single(status => status.Provider.Name == "AniDB UDP");

        banStates.Udp.Ban();
        udp.Raise(h => h.AniDBStateUpdate += null, udp.Object, new AniDBStateUpdate { UpdateType = UpdateType.UDPBan, Value = true });
        udp.Raise(h => h.AniDBStateUpdate += null, udp.Object, new AniDBStateUpdate { UpdateType = UpdateType.LoginFailed, Value = true });

        var ban = udpStatus().Suspensions.Single(suspension => suspension.Kind is SuspensionKind.Banned);
        Assert.True(ban.IsLiftable);
        Assert.Equal(banStates.Udp.BanExpiresUtc, ban.ResumesAt);
        Assert.Null(udpStatus().Suspensions.Single(suspension => suspension.Kind is SuspensionKind.AuthenticationFailed).ResumesAt);

        Assert.True(await service.Lift(udpStatus().Provider.ID, SuspensionKind.Banned, TestContext.Current.CancellationToken));

        Assert.False(banStates.Udp.IsBanned);
        Assert.Equal(SuspensionKind.AuthenticationFailed, Assert.Single(udpStatus().Suspensions).Kind);
    }
}
