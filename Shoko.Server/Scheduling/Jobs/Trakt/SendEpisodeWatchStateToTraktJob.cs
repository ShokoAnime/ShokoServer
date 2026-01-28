using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
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
public class SendEpisodeWatchStateToTraktJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    private SVR_AniDB_Episode _episode;
    public int AnimeEpisodeID { get; set; }
    public TraktSyncType Action { get; set; }

    public override string TypeName => "Send Episode Watch State to Trakt";
    public override string Title => "Sending Episode Watch State to Trakt";
    public override void PostInit()
    {
        _episode = RepoFactory.AnimeEpisode?.GetByID(AnimeEpisodeID)?.AniDB_Episode;
    }

    public override Dictionary<string, object> Details =>
        _episode == null ? new()
        {
            { "EpisodeID", AnimeEpisodeID }
        } : new()
        {
            { "Anime", RepoFactory.AniDB_Anime.GetByAnimeID(_episode.AnimeID)?.PreferredTitle },
            { "Episode Type", ((EpisodeType)_episode.EpisodeType).ToString() },
            { "Episode Number", _episode.EpisodeNumber },
            { "Sync Action", Action.ToString() }
        };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SendEpisodeWatchStateToTraktJob));
        var settings = _settingsProvider.GetSettings();

        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return Task.CompletedTask;

        var episode = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        if (episode == null) return Task.CompletedTask;

        _helper.SendEpisodeWatchState(Action, episode);

        return Task.CompletedTask;
    }

    public SendEpisodeWatchStateToTraktJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SendEpisodeWatchStateToTraktJob() { }
}
