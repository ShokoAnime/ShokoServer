using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all unused (unlinked) releases from the database, optionally
///   filtered by provider.
/// </summary>
public sealed class PurgeAllUnusedReleasesAction(IVideoReleaseService videoReleaseService) : IScheduledAction
{
    public string Name => "Purge All Unused Releases";

    public string? Description => "Remove all unused (unlinked) releases from the database, optionally filtered by provider.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unused releases from the database?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => videoReleaseService.PurgeUnusedReleases(providerNames: null, skipEvents: false);
}
