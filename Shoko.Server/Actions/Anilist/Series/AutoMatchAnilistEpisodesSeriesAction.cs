using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically match the series' episodes with the linked AniList anime's episodes.
/// </summary>
public sealed class AutoMatchAnilistEpisodesSeriesAction(AnilistLinkingService linkingService) : SeriesAction
{
    public override string Name => "Auto-Match AniList Episodes";

    public override string? Description => "Automatically matches Shoko episodes with AniList episodes.";

    public override ActionCategory Category => ActionCategory.AniList;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
    {
        var anilistAnimeId = Series.AnilistAnimeCrossReferences is [{ } first, ..]
            ? first.AnilistAnimeID
            : 0;
        if (anilistAnimeId is 0)
            return Task.CompletedTask;

        linkingService.MatchAnidbToAnilistEpisodes(Series.AnidbAnimeID, anilistAnimeId, useExisting: true, saveToDatabase: true);
        return Task.CompletedTask;
    }
}
