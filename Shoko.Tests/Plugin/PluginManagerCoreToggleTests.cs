using System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Every provider the core registers for itself is attributed to the core plugin entry,
///   because <c>GetPluginInfo(Assembly)</c> resolves a part's plugin through an <c>IPlugin</c>
///   in its assembly -- so for <c>Shoko.Server.dll</c> that is <see cref="CorePlugin"/>. The
///   enable/disable toggle is keyed on the plugin entry, which means the toggle is not
///   "disable one plugin" but "disable the core's participation in every provider service at
///   once": hashing, relocation, and now the stream pipeline's playback observers. It is also
///   not a plugin a user can see (<c>showCorePlugin</c> defaults to false), so refusing the
///   toggle has no visible cost.
/// </summary>
public class PluginManagerCoreToggleTests
{
    [Fact]
    public void CorePlugin_IsNotDisabled()
    {
        var manager = CreateManager();
        var core = PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID);

        Assert.True(manager.DisablePlugin(core).IsEnabled);
    }

    [Fact]
    public void InstalledPlugin_IsDisabled()
    {
        var manager = CreateManager();
        var plugin = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid());

        Assert.False(manager.DisablePlugin(plugin).IsEnabled);
    }

    private static PluginManager CreateManager()
    {
        StubSettingsProvider.Install();

        var systemService = new Mock<ISystemService>();
        systemService.Setup(s => s.Version).Returns(new VersionInformation
        {
            Version = new Version(1, 0, 0, 0),
            RuntimeIdentifier = "win-x64",
            AbstractionVersion = new Version(6, 0, 0, 0),
            SourceRevision = null,
            ReleaseTag = null,
            Channel = ReleaseChannel.Stable,
            ReleasedAt = DateTime.UnixEpoch
        });

        return new PluginManager(NullLogger<PluginManager>.Instance, systemService.Object,
            Mock.Of<IApplicationPaths>());
    }
}
