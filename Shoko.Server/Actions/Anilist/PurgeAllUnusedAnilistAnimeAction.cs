using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove all AniList anime that are no longer linked to any series.
/// </summary>
public sealed class PurgeAllUnusedAnilistAnimeAction(AnilistMetadataService anilistService) : IExecutableAction
{
    public string Name => "Purge All Unused AniList Anime";

    public string? Description => "Remove all AniList anime from the database that are not linked to any series.";

    public ActionCategory Category => ActionCategory.AniList;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unused AniList anime from the database?";

    public Task Execute(CancellationToken token = default)
        => anilistService.PurgeAllUnusedAnime();
}
