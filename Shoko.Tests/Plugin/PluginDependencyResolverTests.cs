using System;
using System.Linq;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Only a plugin that requires another keeps it from being disabled or
///   follows it when it is uninstalled; one that optionally depends on it goes
///   on without it.
/// </summary>
public class PluginDependencyResolverTests
{
    private static readonly Guid _backend = Guid.Parse("00000000-0000-4000-8000-000000000011");

    private static readonly Guid _optional = Guid.Parse("00000000-0000-4000-8000-000000000012");

    private static readonly Guid _required = Guid.Parse("00000000-0000-4000-8000-000000000013");

    private static readonly Guid _onRequired = Guid.Parse("00000000-0000-4000-8000-000000000014");

    private static readonly Guid _onOptional = Guid.Parse("00000000-0000-4000-8000-000000000015");

    private readonly LocalPluginInfo[] _plugins =
    [
        Local(_backend),
        Local(_optional, Depends(_backend, optional: true)),
        Local(_required, Depends(_backend)),
        Local(_onRequired, Depends(_required)),
        Local(_onOptional, Depends(_optional)),
    ];

    [Fact]
    public void OnlyRequiringPluginsBlockADisable()
        => Assert.Equal([_required], Resolver().ValidateDisable(_plugins[0]).Select(plugin => plugin.ID));

    [Fact]
    public void TheUninstallCascadeFollowsOnlyRequiredDependencies()
        => Assert.Equal([_required, _onRequired], Resolver().GetUninstallCascade(_plugins[0]).Select(plugin => plugin.ID).Order());

    private PluginDependencyResolver Resolver()
        => new(Mock.Of<IPluginManager>(manager => manager.GetPluginInfos() == _plugins));

    private static PluginDependency Depends(Guid id, bool optional = false)
        => new() { PluginID = id, VersionRange = ">=1.0.0", IsOptional = optional };

    private static LocalPluginInfo Local(Guid id, params PluginDependency[] dependencies)
        => new()
        {
            ID = id,
            Name = id.ToString(),
            Description = string.Empty,
            Version = new()
            {
                Version = new(1, 0, 0),
                RuntimeIdentifier = "any",
                AbstractionVersion = new(6, 0, 0),
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = true,
            IsActive = true,
            IsPinned = false,
            CanUninstall = true,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [$"{id}.dll"],
            Types = [],
            Dependencies = dependencies,
        };
}
