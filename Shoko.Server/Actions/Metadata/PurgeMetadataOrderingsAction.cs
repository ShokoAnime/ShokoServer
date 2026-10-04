using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove the alternate orderings one metadata source, or every source,
///   keeps.
/// </summary>
/// <remarks>
///   The users' own orderings are kept. A refresh stores a source's
///   orderings again. A source named must be known and keep orderings.
/// </remarks>
/// <param name="orderingService">Keeps the orderings.</param>
public sealed class PurgeMetadataOrderingsAction(MetadataOrderingService orderingService) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal>? _progress;

    /// <summary>
    ///   The source to purge, or <c>null</c> for every source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public string Name => "Purge Metadata Alternate Orderings";

    public string? Description
        => "Removes the alternate orderings of one metadata source, or of every source. The users' own "
            + "orderings are kept.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove the alternate orderings of the chosen metadata sources?";

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(MetadataPurges.Check(Source, orderingService.KeepsGlobalOrderings, "alternate orderings"));

    public Task Execute(CancellationToken token = default)
        => MetadataPurges.ForEach(
            Source is { } source ? [source] : orderingService.GetGlobalOrderingSources(),
            (each, stage, ct) => Task.FromResult(orderingService.RemoveOrderings(each, stage, ct)),
            _progress,
            token
        );
}
