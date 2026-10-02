using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the creators, characters, studios and networks of the plugin
///   sources, and the people and networks of TMDB, that nothing has used for
///   longer than the metadata settings allow.
/// </summary>
/// <remarks>
///   <see cref="PurgeExpiredOrphanedMetadataAction"/> does the same work,
///   daily unless the admin says otherwise.
/// </remarks>
public sealed class PurgeOrphanedMetadataAction(IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge Orphaned Metadata";

    public string? Description => "Removes the people, studios and networks of the plugin metadata sources, and the people and networks of TMDB, that nothing has used for a while.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage
        => "Are you sure you want to remove the people, studios and networks of TMDB and the plugin metadata sources that nothing uses?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => purgeService.PurgeOrphaned(null, null, progress, token);
}
