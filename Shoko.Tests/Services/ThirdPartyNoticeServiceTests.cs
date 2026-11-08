using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.BuildTools.Tasks;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Covers how <see cref="ThirdPartyNoticeService"/> reads the notices files the
///   build tools write, beside the core and in each plugin's folder.
/// </summary>
public class ThirdPartyNoticeServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-notice-service-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private static ThirdPartyNotices.ShippedPackage Package(string id, string version)
        => new(id, version, null);

    private static ThirdPartyNotices.PackageLicense Licence(string expression, string text)
        => new(expression, null, null, null, [text], []);

    private static void Write(string path, ThirdPartyNotices.ManualLicense? project, params (ThirdPartyNotices.ShippedPackage, ThirdPartyNotices.PackageLicense)[] packages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ThirdPartyNotices.Write(path, ThirdPartyNotices.Build(project, packages, [], [], out _));
    }

    [Fact]
    public void TheCoreComesFirst_AndOnlyTheFileBesideEachAssemblyIsRead()
    {
        var app = Directory.CreateDirectory(Path.Combine(_root, "app")).FullName;
        var pluginFolder = Path.Combine(_root, "plugins", "Some");
        Write(Path.Combine(app, ThirdPartyNotices.FileName), new("Shoko.Server", "6.0.0", "MIT", "Shoko's licence", null, null), (Package("Core.Package", "1.0.0"), Licence("MIT", "Core text")));
        Write(Path.Combine(app, "plugins", "Bundled", ThirdPartyNotices.FileName), null, (Package("Not.The.Cores", "1.0.0"), Licence("MIT", "Bundled text")));
        Write(Path.Combine(pluginFolder, ThirdPartyNotices.FileName), new("Some.Plugin", "1.0.0", "MIT", null, null, null), (Package("Shared.Package", "2.0.0"), Licence("Apache-2.0", "Apache text")));
        Write(Path.Combine(pluginFolder, "web", ThirdPartyNotices.FileName), null,
            (Package("Shared.Package", "2.0.0"), Licence("Apache-2.0", "Apache text")),
            (Package("A.Web.Package", "3.0.0"), Licence("ISC", "ISC text")));
        Directory.CreateDirectory(Path.Combine(_root, "plugins", "Broken"));
        File.WriteAllText(Path.Combine(_root, "plugins", "Broken", ThirdPartyNotices.FileName), "not brotli");

        var someID = Guid.NewGuid();
        var brokenID = Guid.NewGuid();
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfos()).Returns([
            PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), brokenID, Path.Combine(_root, "plugins", "Broken"), "Broken.dll"),
            PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), someID, pluginFolder, Path.Combine(pluginFolder, "Some.dll")),
            PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID),
        ]);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ApplicationPath).Returns(app);
        paths.SetupGet(p => p.DataPath).Returns(Path.Combine(_root, "data"));
        var service = new ThirdPartyNoticeService(pluginManager.Object, paths.Object, NullLogger<ThirdPartyNoticeService>.Instance);

        var sections = service.GetSections();

        Assert.Equal([CorePlugin.StaticID, someID], sections.Select(section => section.PluginID));
        var core = sections[0];
        Assert.True(core.IsCore);
        Assert.Equal(("Shoko.Server", ThirdPartyNoticeSource.Project, "MIT"), (core.Project!.ID, core.Project.Source, core.Project.License));
        Assert.Equal(["Core.Package"], core.Packages.Select(notice => notice.ID));
        Assert.Equal("Shoko's licence", service.GetText(Assert.Single(core.Project.TextKeys)));
        var plugin = sections[1];
        Assert.False(plugin.IsCore);
        Assert.Equal(["Shared.Package"], plugin.Packages.Select(notice => notice.ID));
        Assert.Equal(ThirdPartyNoticeSource.NuGet, plugin.Packages[0].Source);
        Assert.Equal("Apache text", service.GetText(Assert.Single(plugin.Packages[0].TextKeys)));
        Assert.Null(service.GetText(ThirdPartyNotices.KeyOf("ISC text")));
        Assert.Null(service.GetText("0000"));
    }
}
