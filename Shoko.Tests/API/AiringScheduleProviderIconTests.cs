using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers an airing schedule provider's icon: the one it declares, else its
/// plugin's, on the provider model and served at its icon route.
/// </summary>
public class AiringScheduleProviderIconTests
{
    #region Fixture

    private sealed class Provider : IAiringScheduleProvider
    {
        public string Name => "Schedule";

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static readonly IApplicationPaths _paths = Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == Path.GetTempPath() && paths.ApplicationPath == AppContext.BaseDirectory);

    private static PackageImageInfo Icon(string path, string mimeType)
        => new() { FilePath = path, MimeType = mimeType, Width = 24, Height = 24 };

    /// <summary>
    /// A service holding one provider, of a plugin with the given icon and
    /// no files of its own to find one in.
    /// </summary>
    private static AiringScheduleService Service(PackageImageInfo? pluginIcon)
    {
        var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetConfigurationInfo<AiringScheduleServiceSettings>()).Returns(configurationInfo);
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(new AiringScheduleServiceSettings());
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
            .Returns(PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), icon: pluginIcon));

        var service = new AiringScheduleService(
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
        service.AddParts([new Provider()]);
        return service;
    }

    private static AiringScheduleController Controller(IAiringScheduleService service)
        => new(
            new StubSettingsProvider(new ServerSettings()),
            Mock.Of<IPluginManager>(),
            service,
            null!,
            null!,
            null!,
            null!,
            _paths,
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

    #endregion

    [Fact]
    public void AProviderWithoutAnIconOfItsOwnShowsItsPluginsSvgOrPng()
    {
        var declared = Icon("/nowhere/own.png", "image/png");
        var svg = Icon("/nowhere/plugin.svg", "image/svg+xml");

        Assert.Same(declared, AiringScheduleService.ChooseIcon(declared, svg));
        Assert.Same(svg, AiringScheduleService.ChooseIcon(null, svg));
        Assert.Null(AiringScheduleService.ChooseIcon(null, Icon("/nowhere/plugin.jpg", "image/jpeg")));
        Assert.Null(AiringScheduleService.ChooseIcon(null, null));
    }

    [Fact]
    public void TheProviderModelSaysWhetherTheProviderHasAnIcon()
    {
        var plain = Assert.Single(Service(null).GetAvailableProviders());
        var withIcon = Assert.Single(Service(Icon("/nowhere/plugin.svg", "image/svg+xml")).GetAvailableProviders());

        Assert.Equal((false, true), (new AiringScheduleProvider(plain).HasIcon, new AiringScheduleProvider(withIcon).HasIcon));
    }

    [Fact]
    public void AProviderIconIsServedWithItsTypeAndSafeHeaders()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            var service = Service(Icon(file, "image/svg+xml"));
            var providerID = Assert.Single(service.GetAvailableProviders()).ID;
            var controller = Controller(service);

            var result = Assert.IsType<FileContentResult>(controller.GetProviderIcon(providerID));

            Assert.Equal("image/svg+xml", result.ContentType);
            Assert.Equal(File.ReadAllBytes(file), result.FileContents);
            Assert.NotNull(result.EntityTag);
            var headers = controller.Response.Headers;
            Assert.Equal(("nosniff", "sandbox"), (headers.XContentTypeOptions.ToString(), headers.ContentSecurityPolicy.ToString()));
            Assert.StartsWith("private", headers.CacheControl.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void AProviderWithoutAnIconOrAnUnknownOneAnswersNotFound()
    {
        var service = Service(null);
        var controller = Controller(service);

        Assert.IsType<NotFoundObjectResult>(controller.GetProviderIcon(Assert.Single(service.GetAvailableProviders()).ID));
        Assert.IsType<NotFoundObjectResult>(controller.GetProviderIcon(Guid.NewGuid()));
    }
}
