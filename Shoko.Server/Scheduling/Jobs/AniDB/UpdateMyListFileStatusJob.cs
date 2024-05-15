using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
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
    public DateTime? WatchedDate { get; set; }

    public override string TypeName => "Update AniDB MyList Status for File";
    public override string Title => "Updating AniDB MyList Status for File";

    public override void PostInit()
    {
        FullFileName = RepoFactory.FileNameHash?.GetByHash(Hash).FirstOrDefault()?.FileName;
    }

    public override Dictionary<string, object> Details => FullFileName != null ? new()
    {
        { "Filename", FullFileName},
        { "Watched", Watched },
        { "Date", WatchedDate }
    } : new()
    {
        { "Hash", Hash },
        { "Watched", Watched },
        { "Date", WatchedDate }
    } ;

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Filename} | {Watched} | {WatchedDate}", nameof(UpdateMyListFileStatusJob), FullFileName, Watched, WatchedDate);

        var settings = _settingsProvider.GetSettings();
        // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
        var vid = RepoFactory.VideoLocal.GetByHash(Hash);
        if (vid == null) return Task.CompletedTask;

        if (vid.GetAniDBFile() != null)
        {
            _logger.LogInformation("Updating File MyList Status: {Hash}|{Size}", vid.Hash, vid.FileSize);
            var request = _requestFactory.Create<RequestUpdateFile>(
                r =>
                {
                    r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                    r.Hash = vid.Hash;
                    r.Size = vid.FileSize;
                    if (!Watched || WatchedDate == null) return;
                    r.IsWatched = true;
                    r.WatchedDate = WatchedDate;
                }
            );

            request.Send();
        }
        else
        {
            // we have a manual link, so get the xrefs and add the episodes instead as generic files
            var xrefs = vid.EpisodeCrossRefs;
            foreach (var episode in xrefs.Select(xref => xref.GetEpisode()).Where(episode => episode != null))
            {
                _logger.LogInformation("Updating Episode MyList Status: AnimeID: {AnimeID}, Episode Type: {Type}, Episode No: {EP}", episode.AnimeID,
                    episode.EpisodeTypeEnum, episode.EpisodeNumber);
                var request = _requestFactory.Create<RequestUpdateEpisode>(
                    r =>
                    {
                        r.State = settings.AniDb.MyList_StorageState.GetMyList_State();
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                        if (!Watched || WatchedDate == null) return;
                        r.IsWatched = true;
                        r.WatchedDate = WatchedDate;
                    }
                );

                request.Send();
            }
        }

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
