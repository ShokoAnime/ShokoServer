using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the hidden channel routes: a list in and out, written channel by
/// channel through the service.
/// </summary>
public class AiringChannelHiddenRouteTests
{
    #region Fixture

    private static readonly IApplicationPaths _paths = Mock.Of<IApplicationPaths>(
        paths => paths.PluginsPath == Path.GetTempPath() && paths.ApplicationPath == AppContext.BaseDirectory
    );

    /// <summary>
    /// A controller over a service holding the given channels, hiding the
    /// ones it is told to.
    /// </summary>
    /// <param name="channels">The channels the service knows.</param>
    /// <param name="hidden">The IDs of the hidden channels, which the service updates.</param>
    /// <returns>The service mock and the controller.</returns>
    private static (Mock<IAiringScheduleService> Service, AiringScheduleController Controller) Build(IReadOnlyList<IAiringChannel> channels, HashSet<Guid> hidden)
    {
        var service = new Mock<IAiringScheduleService>();
        service.Setup(airing => airing.GetAllChannels(null)).Returns(channels);
        service.Setup(airing => airing.GetChannelByID(It.IsAny<Guid>()))
            .Returns((Guid channelID) => channels.FirstOrDefault(channel => channel.ChannelID == channelID));
        service.SetupGet(airing => airing.HiddenChannelIDs).Returns(() => hidden.ToHashSet());
        service.Setup(airing => airing.SetChannelHidden(It.IsAny<IAiringChannel>(), It.IsAny<bool>()))
            .Returns((IAiringChannel channel, bool isHidden) =>
            {
                if (isHidden)
                    hidden.Add(channel.ChannelID);
                else
                    hidden.Remove(channel.ChannelID);
                return channel;
            });
        var controller = new AiringScheduleController(
            new StubSettingsProvider(new ServerSettings()),
            Mock.Of<IPluginManager>(),
            service.Object,
            null!,
            null!,
            null!,
            null!,
            _paths,
            null!,
            null!,
            null!,
            null!,
            null!
        )
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
            },
        };
        return (service, controller);
    }

    private static IAiringChannel Channel()
        => Mock.Of<IAiringChannel>(channel => channel.ChannelID == Guid.NewGuid());

    #endregion

    [Fact]
    public void SetHiddenChannels_HidesTheListedAndShowsTheRest()
    {
        var (shown, kept, hiddenNow) = (Channel(), Channel(), Channel());
        var hidden = new HashSet<Guid> { shown.ChannelID, kept.ChannelID };
        var (service, controller) = Build([shown, kept, hiddenNow], hidden);

        var result = controller.SetHiddenChannels([kept.ChannelID, hiddenNow.ChannelID, kept.ChannelID]);

        Assert.Equal(new[] { kept.ChannelID, hiddenNow.ChannelID }.Order(), result.Value);
        Assert.Equal(result.Value, controller.GetHiddenChannels().Value);
        service.Verify(airing => airing.SetChannelHidden(shown, false), Times.Once);
        service.Verify(airing => airing.SetChannelHidden(kept, true), Times.Once);
        service.Verify(airing => airing.SetChannelHidden(hiddenNow, true), Times.Once);
    }

    [Fact]
    public void SetHiddenChannels_RefusesAnUnknownChannelAndChangesNothing()
    {
        var channel = Channel();
        var (service, controller) = Build([channel], []);

        var result = controller.SetHiddenChannels([channel.ChannelID, Guid.NewGuid()]);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        service.Verify(airing => airing.SetChannelHidden(It.IsAny<IAiringChannel>(), It.IsAny<bool>()), Times.Never);
    }
}
