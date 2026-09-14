using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all AniList anime in the local database from the remote API.
/// </summary>
public sealed class UpdateAllAnilistAnimeAction(AnilistMetadataService anilistService) : IExecutableAction
{
    public string Name => "Update All AniList Anime";

    public string? Description => "Update all AniList anime metadata without downloading images.";

    public ActionCategory Category => ActionCategory.AniList;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => anilistService.UpdateAllAnime(true, false);
}
