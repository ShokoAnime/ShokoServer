using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_UpdateWatchedUDP)]
    public class CommandRequest_UpdateMyListFileStatus : CommandRequestImplementation
    {
        public string FullFileName { get; set; }
        public string Hash { get; set; }
        public bool Watched { get; set; }
        public bool UpdateSeriesStats { get; set; }
        public int WatchedDateAsSecs { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateMyListInfo,
            extraParams = new[] {FullFileName}
        };

        public CommandRequest_UpdateMyListFileStatus()
        {
        }

        public CommandRequest_UpdateMyListFileStatus(string hash, bool watched, bool updateSeriesStats,
            int watchedDateSecs)
        {
            Hash = hash;
            Watched = watched;
            Priority = (int) DefaultPriority;
            UpdateSeriesStats = updateSeriesStats;
            WatchedDateAsSecs = watchedDateSecs;

            GenerateCommandID();
            FullFileName = RepoFactory.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;
        }

        public override void ProcessCommand(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_UpdateMyListFileStatus: {Hash}", Hash);
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            try
            {
                // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
                var vid = RepoFactory.VideoLocal.GetByHash(Hash);
                if (vid == null) return;
                if (vid.GetAniDBFile() != null)
                {
                    if (WatchedDateAsSecs > 0)
                    {
                        var watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                        var request = new RequestUpdateFile
                        {
                            State = ServerSettings.Instance.AniDb.MyList_StorageState.GetMyList_State(),
                            Hash = vid.Hash,
                            Size = vid.FileSize,
                            IsWatched = true,
                            WatchedDate = watchedDate,
                        };
                        request.Execute(handler);
                    }
                    else
                    {
                        var request = new RequestUpdateFile
                        {
                            State = ServerSettings.Instance.AniDb.MyList_StorageState.GetMyList_State(),
                            Hash = vid.Hash,
                            Size = vid.FileSize,
                            IsWatched = false,
                        };
                        request.Execute(handler);
                    }
                }
                else
                {
                    // we have a manual link, so get the xrefs and add the episodes instead as generic files
                    var xrefs = vid.EpisodeCrossRefs;
                    foreach (var episode in xrefs.Select(xref => xref.GetEpisode()).Where(episode => episode != null))
                    {
                        if (WatchedDateAsSecs > 0)
                        {
                            var watchedDate = Commons.Utils.AniDB.GetAniDBDateAsDate(WatchedDateAsSecs);
                            var request = new RequestUpdateEpisode
                            {
                                State = ServerSettings.Instance.AniDb.MyList_StorageState.GetMyList_State(),
                                EpisodeNumber = episode.EpisodeNumber,
                                AnimeID = episode.AnimeID,
                                IsWatched = true,
                                WatchedDate = watchedDate,
                            };
                            request.Execute(handler);
                        }
                        else
                        {
                            var request = new RequestUpdateEpisode
                            {
                                State = ServerSettings.Instance.AniDb.MyList_StorageState.GetMyList_State(),
                                EpisodeNumber = episode.EpisodeNumber,
                                AnimeID = episode.AnimeID,
                                IsWatched = false,
                            };
                            request.Execute(handler);
                        }
                    }
                }

                logger.Info("Updating file list status: {Hash} - {Watched}", vid.Hash, Watched);

                if (!UpdateSeriesStats) return;
                // update watched stats
                var eps = RepoFactory.AnimeEpisode.GetByHash(vid.ED2KHash);
                if (eps.Count > 0)
                {
                    eps.DistinctBy(a => a.AnimeSeriesID).ForEach(a => a.GetAnimeSeries().QueueUpdateStats());
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_UpdateMyListFileStatus: {Hash} - {Ex}", Hash, ex);
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

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                Hash = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Hash");
                Watched = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Watched"));

                string sUpStats = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus",
                    "UpdateSeriesStats");
                if (bool.TryParse(sUpStats, out bool upStats))
                    UpdateSeriesStats = upStats;

                if (
                    int.TryParse(
                        TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "WatchedDateAsSecs"),
                        out int dateSecs))
                    WatchedDateAsSecs = dateSecs;
                FullFileName = RepoFactory.FileNameHash.GetByHash(Hash).FirstOrDefault()?.FileName;
            }

            if (Hash.Trim().Length > 0)
                return true;
            return false;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}