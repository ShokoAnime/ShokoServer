using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers how a client finds the airing schedule service's own configuration:
/// through the service, by the settings type, rather than by a known ID.
/// </summary>
public class AiringScheduleConfigurationTests
{
    #region Fixture

    private static readonly IApplicationPaths _paths = Mock.Of<IApplicationPaths>(
        paths => paths.PluginsPath == Path.GetTempPath() && paths.ApplicationPath == AppContext.BaseDirectory
    );

    /// <summary>
    /// A service whose configuration service knows the settings type by the
    /// given info.
    /// </summary>
    private static AiringScheduleService Service(ConfigurationInfo info, Mock<IConfigurationService> configurationService)
    {
        configurationService.Setup(service => service.GetConfigurationInfo<AiringScheduleServiceSettings>()).Returns(info);
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(new AiringScheduleServiceSettings());
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
            .Returns(PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));
        return new AiringScheduleService(
            NullLogger<AiringScheduleService>.Instance,
            configurationService.Object,
            pluginManager.Object,
            _paths,
            new Mock<IQueueScheduler>().Object,
            new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object),
            new(() => new Mock<IMetadataService>().Object),
            new(() => new Mock<IMetadataCrossReferenceStore>().Object),
            new(() => new Mock<IMetadataLinkingService>().Object)
        );
    }

    private static ConfigurationInfo Info(IConfigurationService configurationService)
        => new(configurationService)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Airing Schedule Service",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = typeof(AiringScheduleServiceSettings),
            ContextualType = typeof(AiringScheduleServiceSettings).ToContextualType(),
            Schema = new JsonSchema(),
            PluginInfo = PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID),
        };

    #endregion

    [Fact]
    public void TheConfigurationRouteAnswersWithTheServicesOwnConfiguration()
    {
        var configurationService = new Mock<IConfigurationService> { DefaultValue = DefaultValue.Mock };
        var info = Info(configurationService.Object);
        var service = Service(info, configurationService);
        var controller = new AiringScheduleController(
            new StubSettingsProvider(new ServerSettings()),
            Mock.Of<IPluginManager>(),
            service,
            null!,
            null!,
            null!,
            null!,
            _paths
        )
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
            },
        };

        Assert.Same(info, service.ConfigurationInfo);
        Assert.Equal((info.ID, info.Name), (controller.GetConfiguration().Value!.ID, controller.GetConfiguration().Value!.Name));
    }
}
