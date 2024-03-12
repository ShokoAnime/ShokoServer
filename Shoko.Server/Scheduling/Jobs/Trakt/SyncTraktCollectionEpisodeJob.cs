using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
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
    public int AnimeEpisodeID { get; set; }
    public TraktSyncAction Action { get; set; }

    public override string TypeName => "Sync Episode to Trakt Collection";
    public override string Title => "Syncing Episode to Trakt Collection";
    public override Dictionary<string, object> Details
    {
        get
        {
            var episode = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID).AniDB_Episode;
            return new()
            {
                { "Anime", RepoFactory.AniDB_Anime.GetByAnimeID(episode.AnimeID) },
                { "Episode Type", ((EpisodeType)episode.EpisodeType).ToString() },
                { "Episode Number", episode.EpisodeNumber }
            };
        }
    }

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
