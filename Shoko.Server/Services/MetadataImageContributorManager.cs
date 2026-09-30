using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Plugin;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Registers the image contributors and keeps the pairs each one is turned
///   off for with the other metadata settings.
/// </summary>
/// <param name="pluginManager">Tells which plugin a contributor belongs to.</param>
/// <param name="configurationProvider">Where the decisions are kept.</param>
/// <param name="scheduler">Queues the removal of links on pairs turned off.</param>
/// <param name="logger">Where refused contributors are reported.</param>
public class MetadataImageContributorManager(
    IPluginManager pluginManager,
    ConfigurationProvider<MetadataServiceSettings> configurationProvider,
    IQueueScheduler scheduler,
    ILogger<MetadataImageContributorManager> logger
) : IMetadataImageContributorManager
{
    #region Constants

    /// <summary>
    ///   How many of a contributor's image jobs may run at once when it sets
    ///   no limit of its own.
    /// </summary>
    public const int DefaultMaxConcurrentJobs = 2;

    #endregion

    #region Fields

    private List<MetadataImageContributorInfo>? _contributors;

    #endregion

    #region Registration

    /// <summary>
    ///   Takes the image contributors the plugins provide, in plugin load
    ///   order, refusing the ones that name no source, an unregistered, core
    ///   or local source or a source another contributor uses, or nothing to
    ///   add images for.
    ///   Called once during start-up; later calls have no effect.
    /// </summary>
    /// <param name="contributors">The contributors.</param>
    public void AddParts(IEnumerable<IMetadataImageContributor> contributors)
    {
        if (_contributors is not null)
            return;

        var registered = new List<MetadataImageContributorInfo>();
        foreach (var contributor in contributors)
        {
            if (Register(contributor, registered) is { } info)
                registered.Add(info);
        }

        _contributors = registered;
        configurationProvider.Saved += (_, _) => ApplySettings();
        ApplySettings();

        if (registered.Count > 0)
            logger.LogInformation(
                "Registered {Count} image contributors: {Contributors}.",
                registered.Count,
                string.Join(", ", registered.Select(info => info.Name))
            );
    }

    /// <summary>
    ///   Builds a contributor's registration, or logs why it is refused.
    /// </summary>
    /// <param name="contributor">The contributor.</param>
    /// <param name="registered">The contributors taken before it.</param>
    /// <returns>The registration, or <see langword="null"/> when it is refused.</returns>
    private MetadataImageContributorInfo? Register(IMetadataImageContributor contributor, List<MetadataImageContributorInfo> registered)
    {
        var contributorType = contributor.GetType();
        if (pluginManager.GetPluginInfo(contributorType.Assembly) is not { } pluginInfo)
        {
            logger.LogWarning("Refusing image contributor {Contributor}: it does not belong to a loaded plugin.", contributor.Name);
            return null;
        }

        MetadataSource source;
        MetadataEntityScope scope;
        try
        {
            source = contributor.Source;
            scope = contributor.Scope;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refusing image contributor {Contributor}: reading its source or scope failed.", contributor.Name);
            return null;
        }

        if (source is null || scope is null)
        {
            logger.LogError("Refusing image contributor {Contributor}: it names no source or no scope.", contributor.Name);
            return null;
        }

        if (source.IsCore)
        {
            logger.LogError("Refusing image contributor {Contributor}: {Source} is a core source.", contributor.Name, source.Value);
            return null;
        }

        if (!source.IsRegistered)
        {
            logger.LogError("Refusing image contributor {Contributor}: no plugin registered {Source}, so none of its images can be linked.", contributor.Name, source.Value);
            return null;
        }

        if (source.IsLocal)
        {
            logger.LogError("Refusing image contributor {Contributor}: {Source} is a local source, which holds no remote images.", contributor.Name, source.Value);
            return null;
        }

        if (registered.FirstOrDefault(info => info.Source == source) is { } existing)
        {
            logger.LogError(
                "Refusing image contributor {Contributor}: image contributor {ExistingContributor} already keeps its images under {Source}.",
                contributor.Name, existing.Name, source.Value
            );
            return null;
        }

        var own = MetadataEntityScope.FromPairs(scope.Where(pair => pair.Source == source));
        if (!own.IsEmpty)
            logger.LogWarning(
                "Image contributor {Contributor} will not add images for {Pairs}: its own source's images come from its provider.",
                contributor.Name, own
            );

        var available = scope.Except(own);
        if (available.IsEmpty)
        {
            logger.LogError("Refusing image contributor {Contributor}: it adds images for nothing.", contributor.Name);
            return null;
        }

        return new()
        {
            ID = UuidUtility.GetV5($"MetadataImageContributor={contributorType.FullName!}", pluginInfo.ID),
            Version = contributor.Version,
            Name = contributor.Name,
            Description = contributor.Description?.CleanDescription() ?? string.Empty,
            Contributor = contributor,
            PluginInfo = pluginInfo,
            Source = source,
            MaxConcurrentJobs = contributor.MaxConcurrentJobs is > 0 and var limit ? limit : DefaultMaxConcurrentJobs,
            AvailableScope = available,
            EnabledScope = available,
        };
    }

    /// <summary>
    ///   Re-reads which pairs each contributor is turned off for. Called once
    ///   the contributors are registered and again whenever the settings are
    ///   saved, so a change takes effect without a restart.
    /// </summary>
    public void ApplySettings()
    {
        if (_contributors is not { Count: > 0 } contributors)
            return;

        var settings = configurationProvider.Load();
        foreach (var info in contributors)
            info.EnabledScope = info.AvailableScope.Except(Disabled(settings, info.ID));
    }

    /// <summary>
    ///   The pairs a contributor is turned off for, as the settings keep them.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <returns>The pairs.</returns>
    private static MetadataEntityScope Disabled(MetadataServiceSettings settings, Guid contributorID)
        => settings.ImageContributors.TryGetValue(contributorID, out var decisions)
            ? MetadataEntityScope.FromPairs(decisions.Disabled.SelectMany(pair => pair.Value.Select(entityType => (pair.Key, entityType))))
            : MetadataEntityScope.Empty;

    #endregion

    #region Contributors

    /// <inheritdoc />
    public IReadOnlyList<MetadataImageContributorInfo> ImageContributors => _contributors ?? [];

    /// <inheritdoc />
    public MetadataImageContributorInfo? GetImageContributorInfo(Guid contributorID)
        => contributorID == Guid.Empty ? null : ImageContributors.FirstOrDefault(info => info.ID == contributorID);

    /// <inheritdoc />
    public MetadataImageContributorInfo GetImageContributorInfo(IMetadataImageContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        return ImageContributors.FirstOrDefault(info => ReferenceEquals(info.Contributor, contributor))
            ?? throw new ArgumentException($"Unregistered image contributor: '{contributor.GetType().Name}'", nameof(contributor));
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataImageContributorInfo> GetImageContributorInfo(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        return [.. ImageContributors.Where(info => info.PluginInfo.ID == plugin.ID)];
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataImageContributorInfo> GetImageContributorsFor(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        return [.. ImageContributors.Where(info => info.EnabledScope.Contains(entityID))];
    }

    #endregion

    #region Settings

    /// <inheritdoc />
    public void SetImageContributorEnabled(Guid contributorID, MetadataEntityScope enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        if (GetImageContributorInfo(contributorID) is not { } info)
            throw new ArgumentException($"No image contributor goes by '{contributorID}'.", nameof(contributorID));

        var unavailable = enabled.Except(info.AvailableScope);
        if (!unavailable.IsEmpty)
            throw new ArgumentException($"{info.Name} cannot add images for {unavailable}.", nameof(enabled));

        var turnedOff = info.EnabledScope.Except(enabled);
        var disabled = info.AvailableScope.Except(enabled);
        var settings = configurationProvider.Load();
        if (Disabled(settings, contributorID).Equals(disabled))
            return;

        if (disabled.IsEmpty)
            settings.ImageContributors.Remove(contributorID);
        else
            settings.ImageContributors[contributorID] = new()
            {
                Disabled = disabled.Sources.ToDictionary(source => source, source => disabled.GetEntityTypes(source).ToHashSet()),
            };

        configurationProvider.Save(settings);
        ApplySettings();

        if (turnedOff.IsEmpty)
            return;

        logger.LogInformation("Image contributor {Contributor} was turned off for {Pairs}; removing its images there.", info.Name, turnedOff);
        scheduler.Enqueue<ClearContributedImagesJob>(job =>
            {
                job.ContributorID = info.ID;
                job.Source = info.Source.Value;
            }, prioritize: true)
            .ContinueWith(
                task => logger.LogError(task.Exception, "Failed to queue the removal of {Contributor}'s images.", info.Name),
                TaskContinuationOptions.OnlyOnFaulted
            );
    }

    #endregion
}
