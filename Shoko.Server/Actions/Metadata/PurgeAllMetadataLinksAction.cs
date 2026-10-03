using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove every link between the AniDB anime and every other metadata
///   source, at every level.
/// </summary>
/// <remarks>
///   The sources of plugins that are gone are cleared too. Nothing the links
///   pointed at is purged, and no anime is told to be left alone, so the
///   next search links them again. <see cref="PurgeMetadataLinksAction"/>
///   does it for one source.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="crossReferences">The links.</param>
/// <param name="linkingService">Removes the links.</param>
public sealed class PurgeAllMetadataLinksAction(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataLinkingService linkingService
) : IScheduledAction
{
    public string Name => "Purge All Metadata Links";

    public string? Description => "Removes every link between AniDB anime and every other metadata source. Links made by hand are lost too, "
        + "and every anime has to be matched again.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage
        => "Are you sure you want to remove every link to every metadata source? Links made by hand are lost too.";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => MetadataPurges.ForEach(
            MetadataPurges.LinkedSources(providerManager, crossReferences),
            (source, stage, ct) => linkingService.RemoveAllLinks(source, true, true, false, stage, ct),
            progress,
            token
        );
}
