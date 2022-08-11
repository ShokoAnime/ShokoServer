using System;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetUpdated)]
    public class CommandRequest_GetUpdated : CommandRequestImplementation
    {
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = "Getting list of updated anime from UDP API",
            queueState = QueueStateEnum.GetUpdatedAnime,
            extraParams = new string[0]
        };

        public CommandRequest_GetUpdated()
        {
        }

        public CommandRequest_GetUpdated(bool forced)
        {
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_GetUpdated");
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            try
            {
                // check the automated update table to see when the last time we ran this command
                var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBUpdates);
                if (sched != null)
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Anime_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                DateTime webUpdateTime;
                if (sched == null)
                {
                    // if this is the first time, lets ask for last 3 days
                    webUpdateTime = DateTime.UtcNow.AddDays(-3);

                    sched = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBUpdates };
                }
                else
                {
                    Logger.LogTrace("Last AniDB info update was : {UpdateDetails}", sched.UpdateDetails);
                    webUpdateTime = DateTime.UnixEpoch.AddSeconds(long.Parse(sched.UpdateDetails));

                    Logger.LogInformation($"{DateTime.UtcNow - webUpdateTime:g} since last UPDATED command");
                }

                var (response, countAnime, countSeries) = Update(webUpdateTime, handler, sched, 0, 0);

                while (response?.Response?.Count > 200)
                    (response, countAnime, countSeries) = Update(response.Response.LastUpdated, handler, sched, countAnime, countSeries);

                Logger.LogInformation("Updating {Count} anime records, and {CountSeries} group status records", countAnime, countSeries);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing CommandRequest_GetUpdated: {Ex}", ex);
            }
        }

        private (UDPResponse<ResponseUpdatedAnime> response, int countAnime, int countSeries) Update(DateTime webUpdateTime, IUDPConnectionHandler handler, ScheduledUpdate sched, int countAnime, int countSeries)
        {
            // get a list of updates from AniDB
            // startTime will contain the date/time from which the updates apply to
            var request = new RequestUpdatedAnime { LastUpdated = webUpdateTime };
            var response = request.Execute(handler);
            if (response?.Response == null) return (null, countAnime, countSeries);
            var animeIDsToUpdate = response.Response.AnimeIDs;

            // now save the update time from AniDB
            // we will use this next time as a starting point when querying the web cache
            sched.LastUpdate = DateTime.Now;
            sched.UpdateDetails = ((int)(response.Response.LastUpdated - DateTime.UnixEpoch).TotalSeconds).ToString();
            RepoFactory.ScheduledUpdate.Save(sched);

            if (animeIDsToUpdate.Count == 0)
            {
                Logger.LogInformation("No anime to be updated");
                return (response, countAnime, countSeries);
            }

            foreach (var animeID in animeIDsToUpdate)
            {
                // update the anime from HTTP
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null)
                {
                    Logger.LogTrace("No local record found for Anime ID: {AnimeID}, so skipping...", animeID);
                    continue;
                }

                Logger.LogInformation("Updating CommandRequest_GetUpdated: {AnimeID} ", animeID);
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);

                // but only if it hasn't been recently updated
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours > 4)
                {
                    var cmdAnime = new CommandRequest_GetAnimeHTTP(animeID, true, false, false);
                    cmdAnime.Save();
                    countAnime++;
                }

                // update the group status
                // this will allow us to determine which anime has missing episodes
                // so we only get by an anime where we also have an associated series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser == null) continue;
                var cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID, true);
                cmdStatus.Save();
                countSeries++;
            }

            return (response, countAnime, countSeries);
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_GetUpdated";
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetUpdated", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
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