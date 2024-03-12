using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class UpdateMyListFileStatusJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;

    private string FullFileName { get; set; }
    public string Hash { get; set; }
    public bool Watched { get; set; }
    public bool UpdateSeriesStats { get; set; }
    public int WatchedDateAsSecs { get; set; }

    public override string TypeName => "Update AniDB MyList";

    public override void PostInit()
    {
        FullFileName = RepoFactory.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;
    }

    public override string Title => "Updating AniDB MyList Status for File";
    public override Dictionary<string, object> Details => new()
    {
        {
            "Filename", FullFileName
        }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Filename} | {Hash} | {Watched}", nameof(UpdateMyListFileStatusJob), FullFileName, Hash, Watched);

        var settings = _settingsProvider.GetSettings();
        // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
        var vid = RepoFactory.VideoLocal.GetByHash(Hash);
        if (vid == null) return Task.CompletedTask;

        if (vid.GetAniDBFile() != null)
        {
            if (Watched && WatchedDateAsSecs > 0)
            {
                var watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                var request = _requestFactory.Create<RequestUpdateFile>(
                    r =>
                    {
                        r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                        r.Hash = vid.Hash;
                        r.Size = vid.FileSize;
                        r.IsWatched = true;
                        r.WatchedDate = watchedDate;
                    }
                );
                request.Send();
            }
            else
            {
                var request = _requestFactory.Create<RequestUpdateFile>(
                    r =>
                    {
                        r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                        r.Hash = vid.Hash;
                        r.Size = vid.FileSize;
                        r.IsWatched = false;
                    }
                );
                request.Send();
            }
        }
        else
        {
            // we have a manual link, so get the xrefs and add the episodes instead as generic files
            var xrefs = vid.EpisodeCrossRefs;
            foreach (var episode in xrefs.Select(xref => xref.GetEpisode()).Where(episode => episode != null))
            {
                if (Watched && WatchedDateAsSecs > 0)
                {
                    var watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                    var request = _requestFactory.Create<RequestUpdateEpisode>(
                        r =>
                        {
                            r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                            r.EpisodeNumber = episode.EpisodeNumber;
                            r.AnimeID = episode.AnimeID;
                            r.IsWatched = true;
                            r.WatchedDate = watchedDate;
                        }
                    );
                    request.Send();
                }
                else
                {
                    var request = _requestFactory.Create<RequestUpdateEpisode>(
                        r =>
                        {
                            r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                            r.EpisodeNumber = episode.EpisodeNumber;
                            r.AnimeID = episode.AnimeID;
                            r.IsWatched = false;
                        }
                    );
                    request.Send();
                }
            }
        }

        _logger.LogInformation("Updating file list status: {Hash} - {Watched}", vid.Hash, Watched);

        if (!UpdateSeriesStats) return Task.CompletedTask;

        // update watched stats
        var eps = RepoFactory.AnimeEpisode.GetByHash(vid.ED2KHash);
        if (eps.Count > 0)
        {
            eps.DistinctBy(a => a.AnimeSeriesID).ForEach(a => a.GetAnimeSeries().QueueUpdateStats());
        }

        return Task.CompletedTask;
    }
    
    public UpdateMyListFileStatusJob(IRequestFactory requestFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
    }

    protected UpdateMyListFileStatusJob()
    {
    }
}
