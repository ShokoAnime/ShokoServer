using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Scan for TMDB matches for all AniDB anime that are not yet linked.
/// </summary>
internal sealed class SearchForTmdbMatchesAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Search for TMDB Matches";
    public string? Description => "Scan for TMDB show and movie matches for all unlinked AniDB anime.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.ScanForMatches();
    }
}
