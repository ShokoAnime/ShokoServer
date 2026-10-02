using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the stored series and films of one metadata source, or of every
///   source, TMDB included, that nothing links to any more, and the
///   collections none of whose members anything links to.
/// </summary>
/// <remarks>
///   Only queues the purges, which run on their own; the progress covers
///   the queuing. A source named must be known and keep entries of its own.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="purgeService">Queues the purges.</param>
public sealed class PurgeUnusedMetadataAction(
    IMetadataProviderManager providerManager,
    IMetadataPurgeService purgeService
) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal>? _progress;

    /// <summary>
    ///   The source to purge, or <see langword="null"/> for every source, TMDB
    ///   included.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public string Name => "Purge Unused Metadata";

    public string? Description
        => "Removes the stored series, films and collections of one metadata source, or of every source, TMDB included, that are not linked to "
            + "any AniDB anime.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove the unlinked series, films and collections of the chosen metadata sources?";

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(MetadataPurges.Check(Source, source => MetadataPurges.IsPurgeable(providerManager, source), "stored series, films or collections"));

    public Task Execute(CancellationToken token = default)
        => MetadataPurges.ForEach(
            Source is { } source ? [source] : MetadataPurges.StoredSources(providerManager),
            (each, stage, ct) => purgeService.PurgeUnused(each, null, null, stage, ct),
            _progress,
            token
        );
}
