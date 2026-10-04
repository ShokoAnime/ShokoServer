using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the stored series and films of one metadata source, or of every
///   source, that nothing links to any more, and the collections none of
///   whose members anything links to.
/// </summary>
/// <remarks>
///   Only queues the purges, which run on their own; the progress covers
///   the queuing. A source named must be known and keep entries of its own,
///   and a kind named must be series, movies or collections.
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
    ///   The source to purge, or <c>null</c> for every source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   Purge only series, only movies or only collections, or
    ///   <c>null</c> for all three.
    /// </summary>
    public MetadataEntityType? EntityType { get; set; }

    public string Name => "Purge Unused Metadata";

    public string? Description
        => "Removes the stored series, films and collections of one metadata source, or of every source, that are not linked to "
            + "any AniDB anime.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove the unlinked series, films and collections of the chosen metadata sources?";

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(
            MetadataPurges.CheckKind(EntityType)
            ?? MetadataPurges.Check(Source, source => MetadataPurges.IsPurgeable(providerManager, source), "stored series, films or collections")
        );

    public Task Execute(CancellationToken token = default)
        => MetadataPurges.ForEach(
            Source is { } source ? [source] : MetadataPurges.StoredSources(providerManager),
            (each, stage, ct) => purgeService.PurgeUnused(each, null, EntityType, stage, ct),
            _progress,
            token
        );
}
