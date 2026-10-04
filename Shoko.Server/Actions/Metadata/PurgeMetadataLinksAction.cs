using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove every link between the AniDB anime and one metadata source, or
///   every other source, at every level.
/// </summary>
/// <remarks>
///   Nothing the links pointed at is purged, and no anime is told to be left
///   alone, so the next search links them again. A source named must be known
///   and able to link to AniDB.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="crossReferences">The links.</param>
/// <param name="linkingService">Removes the links.</param>
public sealed class PurgeMetadataLinksAction(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataLinkingService linkingService
) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal>? _progress;

    /// <summary>
    ///   The source to purge, or <c>null</c> for every source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public string Name => "Purge Metadata Links";

    public string? Description
        => "Removes every link between AniDB anime and one metadata source, or every other source. Links made by hand are lost "
            + "too, and every anime has to be matched again.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove every link to the chosen metadata sources? Links made by hand are lost too.";

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(MetadataPurges.Check(Source, source => MetadataPurges.IsPurgeable(providerManager, source), "links to AniDB"));

    public Task Execute(CancellationToken token = default)
        => MetadataPurges.ForEach(
            Source is { } source ? [source] : MetadataPurges.LinkedSources(providerManager, crossReferences),
            (each, stage, ct) => linkingService.RemoveAllLinks(each, true, true, false, stage, ct),
            _progress,
            token
        );
}
