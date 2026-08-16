using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan for TMDB matches for all AniDB anime that are not yet linked.
/// </summary>
public sealed class SearchForTmdbMatchesAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Search for TMDB Matches";

    public string? Description => "Scan for TMDB show and movie matches for all unlinked AniDB anime.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => tmdbService.ScanForMatches();
}
