using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SyncTraktCollectionJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public bool ForceRefresh { get; set; }

    public override string Name => "Sync Trakt Collection";

    public override QueueStateStruct Description => new()
    {
        message = "Syncing Trakt Collection",
        queueState = QueueStateEnum.SyncTrakt,
        extraParams = Array.Empty<string>()
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncTraktCollectionJob));
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.TraktSync, UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.TraktTv.SyncFrequency);

            // if we have run this in the last xxx hours then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !ForceRefresh) return Task.CompletedTask;
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

        _helper.SyncCollectionToTrakt();

        return Task.CompletedTask;
    }

    public SyncTraktCollectionJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SyncTraktCollectionJob() { }
}
