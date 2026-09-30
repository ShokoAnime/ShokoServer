using System;
using System.Collections.Generic;
using System.Reflection;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.IntegrationTests.PluginDatabases;

/// <summary>
/// The plugin the sample database belongs to. It is never loaded; each test
/// gives it an ID of its own through <see cref="SamplePluginManager"/>, so its
/// tables never meet another test's.
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    /// <summary>
    /// The plugin's ID, which the tests replace.
    /// </summary>
    public Guid ID => Guid.Empty;

    public string Name => "Sample Plugin";
}

/// <summary>
/// The server's plugin manager, answering for <see cref="SamplePlugin"/> as if
/// it were loaded with a given ID.
/// </summary>
public class SamplePluginManager : DispatchProxy
{
    private IPluginManager _inner = null!;

    private LocalPluginInfo _pluginInfo = null!;

    /// <summary>
    /// Wraps the server's plugin manager.
    /// </summary>
    /// <param name="inner">The server's plugin manager.</param>
    /// <param name="pluginID">The ID to give <see cref="SamplePlugin"/>.</param>
    /// <returns>The plugin manager.</returns>
    public static IPluginManager Create(IPluginManager inner, Guid pluginID)
    {
        var proxy = Create<IPluginManager, SamplePluginManager>();
        var manager = (SamplePluginManager)(object)proxy;
        manager._inner = inner;
        manager._pluginInfo = new()
        {
            ID = pluginID,
            Name = "Sample Plugin",
            Description = string.Empty,
            Version = new()
            {
                Version = new(1, 0, 0, 0),
                RuntimeIdentifier = "linux-x64",
                AbstractionVersion = new(6, 0, 0, 0),
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
            IsActive = true,
            CanUninstall = true,
            Plugin = null,
            PluginType = typeof(SamplePlugin),
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = new List<string> { "Sample.dll" },
            Types = [],
            Dependencies = [],
        };
        return proxy;
    }

    /// <summary>
    /// Answers for <see cref="SamplePlugin"/>, and passes everything else on.
    /// </summary>
    /// <param name="targetMethod">The method called.</param>
    /// <param name="args">Its arguments.</param>
    /// <returns>What it returns.</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod is { Name: nameof(IPluginManager.GetPluginInfo), IsGenericMethod: true } && targetMethod.GetGenericArguments()[0] == typeof(SamplePlugin)
            ? _pluginInfo
            : targetMethod!.Invoke(_inner, args);
}
