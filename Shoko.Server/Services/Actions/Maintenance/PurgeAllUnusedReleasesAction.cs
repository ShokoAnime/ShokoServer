using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Purge all unused (unlinked) releases from the database, optionally
///   filtered by provider.
/// </summary>
public sealed class PurgeAllUnusedReleasesAction(IVideoReleaseService releaseService) : IExecutableGlobalSystemAction
{
    public string Name => "Purge Unused Releases";
    public string? Description => "Remove all unused (unlinked) releases from the database, optionally filtered by provider.";
    public ActionCategory Category => ActionCategory.Destructive;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await releaseService.PurgeUnusedReleases();
    }
}
