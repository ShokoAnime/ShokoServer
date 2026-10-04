using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge every stored collection of one metadata source, or of every
///   source.
/// </summary>
/// <remarks>
///   Nothing links a collection, so no link is lost. Only queues the purges,
///   which run on their own; the progress covers the queuing. A source named
///   must be known and keep entries of its own.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="purgeService">Queues the purges.</param>
public sealed class PurgeMetadataCollectionsAction(
    IMetadataProviderManager providerManager,
    IMetadataPurgeService purgeService
) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal>? _progress;

    /// <summary>
    ///   The source to purge, or <c>null</c> for every source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public string Name => "Purge Metadata Collections";

    public string? Description
        => "Removes every stored collection of one metadata source, or of every source. A refresh of a series or film a "
            + "collection holds stores it again.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove every collection of the chosen metadata sources?";

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(MetadataPurges.Check(Source, source => MetadataPurges.IsPurgeable(providerManager, source), "stored collections"));

    public Task Execute(CancellationToken token = default)
        => MetadataPurges.ForEach(
            Source is { } source ? [source] : MetadataPurges.StoredSources(providerManager),
            (each, stage, ct) => purgeService.PurgeCollections(each, stage, ct),
            _progress,
            token
        );
}
