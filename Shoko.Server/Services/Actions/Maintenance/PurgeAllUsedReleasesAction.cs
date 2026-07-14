using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Purge all used (linked) releases from the database, optionally filtered
///   by provider.
/// </summary>
internal sealed class PurgeAllUsedReleasesAction(IVideoReleaseService releaseService) : IExecutableGlobalSystemAction
{
    public string Name => "Purge Used Releases";
    public string? Description => "Remove all used (linked) releases from the database, optionally filtered by provider.";
    public ActionCategory Category => ActionCategory.Destructive;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await releaseService.PurgeUsedReleases();
    }
}
