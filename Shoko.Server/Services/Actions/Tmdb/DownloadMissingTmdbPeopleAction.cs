using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Download any missing TMDB person (cast/crew) data.
/// </summary>
public sealed class DownloadMissingTmdbPeopleAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Download Missing TMDB People";
    public string? Description => "Download any missing TMDB person (cast and crew) data.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.RepairMissingPeople();
    }
}
