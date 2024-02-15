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
public class SyncTraktCollectionEpisodeJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public virtual int AnimeEpisodeID { get; set; }
    public virtual TraktSyncAction Action { get; set; }

    public override string Name => "Sync Episode to Trakt Collection";

    public override QueueStateStruct Description => new()
    {
        message = "Sync episode to collection on Trakt: {0} - {1}",
        queueState = QueueStateEnum.SyncTraktEpisodes,
        extraParams = new[] { AnimeEpisodeID.ToString(), Action.ToString() }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> Episode: {Episode} | Action: {Action}", nameof(SyncTraktCollectionEpisodeJob), AnimeEpisodeID, Action);
        var settings = _settingsProvider.GetSettings();

        if (!settings.TraktTv.Enabled ||
            string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return Task.CompletedTask;
        }

        var ep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        if (ep != null)
        {
            var syncType = TraktSyncType.CollectionAdd;
            if (Action == TraktSyncAction.Remove)
            {
                syncType = TraktSyncType.CollectionRemove;
            }

            _helper.SyncEpisodeToTrakt(ep, syncType);
        }

        return Task.CompletedTask;
    }

    public SyncTraktCollectionEpisodeJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SyncTraktCollectionEpisodeJob() { }
}
