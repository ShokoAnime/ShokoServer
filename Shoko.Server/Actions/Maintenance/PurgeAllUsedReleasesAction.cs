using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all used (linked) releases from the database, optionally filtered
///   by provider.
/// </summary>
public sealed class PurgeAllUsedReleasesAction(IVideoReleaseService videoReleaseService) : IExecutableAction
{
    public string Name => "Purge All Used Releases";

    public string? Description => "Remove all used (linked) releases from the database, optionally filtered by provider.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all used releases from the database?";

    public Task Execute(CancellationToken token = default)
        => videoReleaseService.PurgeUsedReleases(providerNames: null, skipEvents: false);
}
