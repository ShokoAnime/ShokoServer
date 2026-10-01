using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh all AniDB anime info from the remote API.
/// </summary>
/// <param name="anidbService">Schedules the refreshes.</param>
/// <param name="anidbAnimes">The AniDB anime.</param>
public sealed class UpdateAllAnidbInfoAction(IAnidbService anidbService, AniDB_AnimeRepository anidbAnimes) : IScheduledAction
{
    public string Name => "Update All AniDB Info";

    public string? Description => "Refresh all AniDB anime information from the remote API.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipSupplementaryUpdate;
        foreach (var anime in anidbAnimes.GetAll())
        {
            token.ThrowIfCancellationRequested();
            await anidbService.ScheduleRefreshOfAnime(anime, refreshMethod).ConfigureAwait(false);
        }
    }
}
