using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.Suspension;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API.Suspension;

/// <summary>
/// Covers lifting a suspension through the API.
/// </summary>
public class SuspensionControllerTests
{
    [Fact]
    public async Task OnlyALiftableSuspensionOfAKnownProviderIsLifted()
    {
        var status = SuspensionTestDoubles.Status(DateTime.UtcNow.AddHours(1), kind: SuspensionKind.Banned);
        var service = SuspensionTestDoubles.Service();
        service.Setup(s => s.GetProviderInfo(status.Provider.ID)).Returns(status.Provider);
        service.Setup(s => s.Get(status.Provider.ID)).Returns(status);
        service.Setup(s => s.Lift(status.Provider.ID, SuspensionKind.Banned, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = new SuspensionController(new StubSettingsProvider(new ServerSettings()), service.Object)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
            },
        };

        var unknown = await controller.LiftSuspension(Guid.NewGuid(), SuspensionKind.Banned, TestContext.Current.CancellationToken);
        var notLiftable = await controller.LiftSuspension(status.Provider.ID, SuspensionKind.Overloaded, TestContext.Current.CancellationToken);
        var lifted = await controller.LiftSuspension(status.Provider.ID, SuspensionKind.Banned, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(unknown.Result);
        Assert.Equal(400, Assert.IsType<ObjectResult>(notLiftable.Result, exactMatch: false).StatusCode);
        Assert.Equal(SuspensionKind.Banned, Assert.Single(Assert.IsType<SuspensionProviderStatus>(lifted.Value).Suspensions).Kind);
    }
}
