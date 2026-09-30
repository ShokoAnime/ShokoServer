using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Builds a <see cref="PluginManager"/> for unit tests, over stub settings and
/// a system service of a fixed version, with no plugins found yet.
/// </summary>
internal static class TestPluginManager
{
    private static readonly FieldInfo _pluginTypesField = typeof(PluginManager)
        .GetField("_pluginTypes", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Creates a plugin manager.
    /// </summary>
    /// <param name="paths">The server's folders, or a mock that gives none.</param>
    /// <returns>The manager.</returns>
    public static PluginManager Create(IApplicationPaths? paths = null)
    {
        StubSettingsProvider.Install();
        var systemService = new Mock<ISystemService>();
        systemService.Setup(s => s.Version).Returns(new VersionInformation
        {
            Version = new Version(1, 0, 0, 0),
            RuntimeIdentifier = "linux-x64",
            AbstractionVersion = new Version(6, 0, 0, 0),
            SourceRevision = null,
            ReleaseTag = null,
            Channel = ReleaseChannel.Stable,
            ReleasedAt = DateTime.UnixEpoch,
        });

        return new PluginManager(NullLogger<PluginManager>.Instance, systemService.Object, paths ?? Mock.Of<IApplicationPaths>());
    }

    /// <summary>
    /// Gets the list a plugin manager keeps the plugins it found in, to add
    /// plugins to without loading them from disk.
    /// </summary>
    /// <param name="manager">The plugin manager.</param>
    /// <returns>The manager's own list.</returns>
    public static List<LocalPluginInfo> Plugins(PluginManager manager)
        => (List<LocalPluginInfo>)_pluginTypesField.GetValue(manager)!;
}
