using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all AniList anime in the local database from the remote API,
///   including downloading any missing images.
/// </summary>
public sealed class UpdateAllAnilistAnimeWithImagesAction(AnilistMetadataService anilistService) : IExecutableAction
{
    public string Name => "Update All AniList Anime (with Images)";

    public string? Description => "Update all AniList anime metadata and download any missing images.";

    public ActionCategory Category => ActionCategory.AniList;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => anilistService.UpdateAllAnime(true, true);
}
