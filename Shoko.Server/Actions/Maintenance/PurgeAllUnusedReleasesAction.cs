using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all unused (unlinked) releases from the database, optionally
///   filtered by provider.
/// </summary>
public sealed class PurgeAllUnusedReleasesAction(IVideoReleaseService videoReleaseService) : IExecutableAction
{
    public string Name => "Purge All Unused Releases";

    public string? Description => "Remove all unused (unlinked) releases from the database, optionally filtered by provider.";

    public ActionCategory Category => ActionCategory.Destructive;

    // Matches today: [Authorize("admin")] on the legacy PurgeAllUnusedReleases endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
        => videoReleaseService.PurgeUnusedReleases(providerNames: null, skipEvents: false);
}
