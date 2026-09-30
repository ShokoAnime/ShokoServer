using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Queues the purge of the series, films and collections nothing has linked
///   to since before the admin's cutoff, for TMDB and every plugin source.
///   Runs daily.
/// </summary>
/// <param name="settingsProvider">Holds how long an unused entry may stay.</param>
/// <param name="providerManager">The registered providers, which decide which core sources are purged.</param>
/// <param name="purgeService">Queues the purges.</param>
/// <param name="cancellationAccessor">Cancels the work.</param>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class PurgeUnusedMetadataJob(
    ISettingsProvider settingsProvider,
    IMetadataProviderManager providerManager,
    IMetadataPurgeService purgeService,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob
{
    #region Constants

    /// <summary>
    ///   The environment variable that sets
    ///   <see cref="MetadataSettings.AutoPurgeUnlinkedAfterDays"/>.
    /// </summary>
    internal const string EnvironmentVariable = "METADATA_AUTO_PURGE_UNLINKED_AFTER_DAYS";

    /// <summary>
    ///   The environment variable that set the days when the purge only
    ///   covered TMDB. Still honoured while the current one is unset.
    /// </summary>
    internal const string LegacyEnvironmentVariable = "TMDB_AUTO_PURGE_UNLINKED_AFTER_DAYS";

    #endregion

    #region Properties

    /// <inheritdoc />
    public override string TypeName => "Purge Unused Metadata";

    /// <inheritdoc />
    public override string Title => "Purging Unused Metadata";

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        var days = GetDays(settingsProvider.GetSettings().Metadata.AutoPurgeUnlinkedAfterDays, Environment.GetEnvironmentVariable, out var legacy);
        if (legacy)
            _logger.LogWarning(
                "{Legacy} is deprecated and now covers every source, not only TMDB. Set {Current} instead.",
                LegacyEnvironmentVariable,
                EnvironmentVariable
            );

        if (days <= 0)
        {
            _logger.LogTrace("Not purging unused metadata: the automatic purge is off.");
            return;
        }

        var cutoff = DateTime.Now.AddDays(-days);
        var queued = 0;
        foreach (var source in GetPurgeableSources(MetadataSource.All, providerManager.MetadataProviders))
        {
            cancellationAccessor.Token.ThrowIfCancellationRequested();
            queued += await purgeService.PurgeUnused(source, cutoff, cancellationToken: cancellationAccessor.Token).ConfigureAwait(false);
        }

        _logger.LogDebug("Queued the purge of {Count} unused entries last refreshed before {Cutoff}.", queued, cutoff);
    }

    /// <summary>
    ///   The number of days an unused entry may stay: the setting, or the
    ///   legacy TMDB environment variable while it is set and the current one
    ///   is not, so an install that turned the purge off through it keeps it
    ///   off.
    /// </summary>
    /// <param name="configured">The value of <see cref="MetadataSettings.AutoPurgeUnlinkedAfterDays"/>.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable by name.</param>
    /// <param name="legacy">Set when the legacy environment variable gave the value.</param>
    /// <returns>The number of days, where <c>0</c> or less turns the purge off.</returns>
    internal static int GetDays(int configured, Func<string, string?> getEnvironmentVariable, out bool legacy)
    {
        legacy = false;
        if (!string.IsNullOrWhiteSpace(getEnvironmentVariable(EnvironmentVariable)))
            return configured;
        if (!int.TryParse(getEnvironmentVariable(LegacyEnvironmentVariable)?.Trim().Trim('"', '\''), out var days))
            return configured;

        legacy = true;
        return days;
    }

    /// <summary>
    ///   The sources whose unused entries the job purges: every registered
    ///   plugin source, and each of the core's sources a provider claims,
    ///   which is TMDB.
    /// </summary>
    /// <param name="sources">The registered sources.</param>
    /// <param name="providers">The registered providers.</param>
    /// <returns>The sources to purge, in the order given.</returns>
    internal static IReadOnlyList<MetadataSource> GetPurgeableSources(IEnumerable<MetadataSource> sources, IEnumerable<MetadataProviderInfo> providers)
    {
        var claimed = providers.Select(info => info.Source).ToHashSet();
        return sources.Where(source => !source.IsCore || claimed.Contains(source)).Distinct().ToList();
    }

    #endregion
}
