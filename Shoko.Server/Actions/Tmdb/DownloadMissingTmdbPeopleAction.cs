using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Download any missing TMDB person (cast/crew) data.
/// </summary>
public sealed class DownloadMissingTmdbPeopleAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Download Missing TMDB People";

    public string? Description => "Download any missing TMDB person (cast and crew) data.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => tmdbService.RepairMissingPeople();
}
