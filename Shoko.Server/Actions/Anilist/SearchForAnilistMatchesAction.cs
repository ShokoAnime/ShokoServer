using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan for AniList matches for all AniDB anime that are not yet linked.
/// </summary>
public sealed class SearchForAnilistMatchesAction(AnilistMetadataService anilistService) : IExecutableAction
{
    public string Name => "Search for AniList Matches";

    public string? Description => "Scan for AniList anime matches for all unlinked AniDB anime.";

    public ActionCategory Category => ActionCategory.AniList;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => anilistService.ScanForMatches();
}
