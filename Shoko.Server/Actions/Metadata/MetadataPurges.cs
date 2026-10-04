using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   What the purges across every metadata source share: the sources each
///   covers, the check of a source a person names, and one purge per source.
/// </summary>
internal static class MetadataPurges
{
    #region Sources

    /// <summary>
    ///   The sources a metadata provider serves, which keep the series,
    ///   films and collections the core may purge.
    /// </summary>
    /// <param name="providerManager">Lists the metadata providers.</param>
    /// <returns>The sources.</returns>
    public static IReadOnlyList<MetadataSource> StoredSources(IMetadataProviderManager providerManager)
        => providerManager.MetadataProviders.Select(info => info.Source).Distinct().ToList();

    /// <summary>
    ///   The sources keeping entries, and every source with a link to AniDB,
    ///   a plugin's that is gone included.
    /// </summary>
    /// <param name="providerManager">Lists the metadata providers.</param>
    /// <param name="crossReferences">The links.</param>
    /// <returns>The sources.</returns>
    public static IReadOnlyList<MetadataSource> LinkedSources(IMetadataProviderManager providerManager, IMetadataCrossReferenceStore crossReferences)
        => StoredSources(providerManager)
            .Concat(crossReferences.GetAllSeriesLinks().Select(link => link.Source))
            .Concat(crossReferences.GetAllMovieLinks().Select(link => link.Source))
            .Concat(crossReferences.GetAllEpisodeLinks().Select(link => link.Source))
            .Distinct()
            .ToList();

    /// <summary>
    ///   Whether a source keeps entries or links the core may purge.
    /// </summary>
    /// <param name="providerManager">Lists the metadata providers.</param>
    /// <param name="source">The source.</param>
    /// <returns><c>true</c> when it does.</returns>
    public static bool IsPurgeable(IMetadataProviderManager providerManager, MetadataSource source)
        => MetadataProviderScheduler.IsPurgeable(source, providerManager.MetadataProviders);

    #endregion

    #region Checks

    /// <summary>
    ///   Refuses a source a person named that is not known or keeps nothing
    ///   of the kind purged.
    /// </summary>
    /// <param name="source">The source, or <c>null</c> for every source.</param>
    /// <param name="keeps">Whether a known source keeps what is purged.</param>
    /// <param name="what">What is purged, e.g. <c>alternate orderings</c>.</param>
    /// <returns>Why the source is refused, or <c>null</c> to allow it.</returns>
    public static ActionValidationResult? Check(MetadataSource? source, Func<MetadataSource, bool> keeps, string what)
        => source switch
        {
            null => null,
            { IsRegistered: false } => new($"\"{source}\" is not a known metadata source."),
            _ when !keeps(source) => new($"{source.Name} keeps no {what}."),
            _ => null,
        };

    /// <summary>
    ///   Refuses a kind a person named that is not a series, a movie or a
    ///   collection, the kinds stored whole.
    /// </summary>
    /// <param name="entityType">The kind, or <c>null</c> for all three.</param>
    /// <returns>Why the kind is refused, or <c>null</c> to allow it.</returns>
    public static ActionValidationResult? CheckKind(MetadataEntityType? entityType)
        => entityType is null || entityType == MetadataEntityType.Series || entityType == MetadataEntityType.Movie ||
            entityType == MetadataEntityType.Collection
            ? null
            : new($"\"{entityType}\" is not a series, a movie or a collection.");

    #endregion

    #region Running

    /// <summary>
    ///   Runs a purge for each source in turn, each source a stage of the
    ///   progress.
    /// </summary>
    /// <param name="sources">The sources.</param>
    /// <param name="purge">Purges one source, reporting into its stage.</param>
    /// <param name="progress">Told how far the purges are, from 0 to 100.</param>
    /// <param name="token">Stops the purges.</param>
    /// <returns>How many entries, links or orderings were purged or queued for purging.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public static async Task<int> ForEach(
        IReadOnlyList<MetadataSource> sources,
        Func<MetadataSource, IProgress<decimal>, CancellationToken, Task<int>> purge,
        IProgress<decimal>? progress,
        CancellationToken token
    )
    {
        var stages = new StagedProgress(progress, Math.Max(sources.Count, 1));
        stages.Report(0);
        var total = 0;
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            total += await purge(source, stages, token).ConfigureAwait(false);
            stages.NextStage();
        }

        stages.Complete();
        return total;
    }

    #endregion
}
