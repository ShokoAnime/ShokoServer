using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SyncTraktEpisodeHistoryJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public int AnimeEpisodeID { get; set; }
    public TraktSyncAction Action { get; set; } = TraktSyncAction.Add;

    public override string Name => "Sync Trakt Episode History";

    public override QueueStateStruct Description => new()
    {
        message = "Add episode to history on Trakt: {0}",
        queueState = QueueStateEnum.TraktAddHistory,
        extraParams = new[] { AnimeEpisodeID.ToString() }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncTraktEpisodeHistoryJob));
        var settings = _settingsProvider.GetSettings();

        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var ep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        if (ep == null) return Task.CompletedTask;

        var syncType = TraktSyncType.HistoryAdd;
        if (Action == TraktSyncAction.Remove) syncType = TraktSyncType.HistoryRemove;

        _helper.SyncEpisodeToTrakt(ep, syncType);

        return Task.CompletedTask;
    }

    public SyncTraktEpisodeHistoryJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SyncTraktEpisodeHistoryJob() { }
}
