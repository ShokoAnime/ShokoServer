using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_UpdateWatchedUDP)]
public class CommandRequest_UpdateMyListFileStatus : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;

    public virtual string FullFileName { get; set; }
    public virtual string Hash { get; set; }
    public virtual bool Watched { get; set; }
    public virtual bool UpdateSeriesStats { get; set; }
    public virtual int WatchedDateAsSecs { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating MyList info from UDP API for File: {0}",
        queueState = QueueStateEnum.UpdateMyListInfo,
        extraParams = new[] { FullFileName }
    };

    public override CommandConflict ConflictBehavior { get; } = CommandConflict.Replace;

    public override void PostInit()
    {
        FullFileName = RepoFactory.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_UpdateMyListFileStatus: {Hash}", Hash);
        FullFileName = RepoFactory.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;

        var settings = _settingsProvider.GetSettings();
        // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
        var vid = RepoFactory.VideoLocal.GetByHash(Hash);
        if (vid == null)
        {
            return;
        }

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
                request.Execute();
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
                request.Execute();
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
                    request.Execute();
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
                    request.Execute();
                }
            }
        }

        Logger.LogInformation("Updating file list status: {Hash} - {Watched}", vid.Hash, Watched);

        if (!UpdateSeriesStats)
        {
            return;
        }

        // update watched stats
        var eps = RepoFactory.AnimeEpisode.GetByHash(vid.ED2KHash);
        if (eps.Count > 0)
        {
            eps.DistinctBy(a => a.AnimeSeriesID).ForEach(a => a.GetAnimeSeries().QueueUpdateStats());
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_UpdateMyListFileStatus_{Hash}_{Guid.NewGuid().ToString()}";
    }

    public override bool LoadFromCommandDetails()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        Hash = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Hash");
        Watched = bool.Parse(
            TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Watched"));

        var sUpStats = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus",
            "UpdateSeriesStats");
        if (bool.TryParse(sUpStats, out var upStats))
        {
            UpdateSeriesStats = upStats;
        }

        if (
            int.TryParse(
                TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "WatchedDateAsSecs"),
                out var dateSecs))
        {
            WatchedDateAsSecs = dateSecs;
        }

        return Hash.Trim().Length > 0;
    }

    public CommandRequest_UpdateMyListFileStatus(ILoggerFactory loggerFactory, IRequestFactory requestFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_UpdateMyListFileStatus()
    {
    }
}
