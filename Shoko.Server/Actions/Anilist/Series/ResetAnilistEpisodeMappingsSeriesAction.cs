using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Reset all AniList episode mappings for the series.
/// </summary>
public sealed class ResetAnilistEpisodeMappingsSeriesAction(AnilistLinkingService linkingService) : SeriesAction
{
    public override string Name => "Reset AniList Episode Mappings";

    public override string? Description => "Reset all AniList episode mappings for the series.";

    public override ActionCategory Category => ActionCategory.AniList;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => "Are you sure you want to reset all AniList episode mappings for this series?";

    public override Task Execute(CancellationToken token = default)
    {
        linkingService.ResetAllEpisodeLinks(Series.AnidbAnimeID, true);
        return Task.CompletedTask;
    }
}
