using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetReleaseGroupStatus)]
    public class CommandRequest_GetReleaseGroupStatus : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetReleaseGroup,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_GetReleaseGroupStatus()
        {
        }

        public CommandRequest_GetReleaseGroupStatus(int aid, bool forced)
        {
            AnimeID = aid;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_GetReleaseGroupStatus: {AnimeID}", AnimeID);
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            try
            {
                // only get group status if we have an associated series
                var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
                if (series == null) return;

                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                // don't get group status if the anime has already ended more than 50 days ago
                if (ShouldSkip(anime))
                {
                    Logger.LogInformation("Skipping group status command because anime has already ended: {AnimeID}", AnimeID);
                    return;
                }

                var request = new RequestReleaseGroupStatus { AnimeID = AnimeID };
                var response = request.Execute(handler);
                if (response.Response == null) return;

                var maxEpisode = response.Response.Max(a => a.LastEpisodeNumber);

                // delete existing records
                RepoFactory.AniDB_GroupStatus.DeleteForAnime(AnimeID);

                // save the records
                var toSave = response.Response.Select(
                    raw => new AniDB_GroupStatus
                    {
                        AnimeID = raw.AnimeID,
                        GroupID = raw.GroupID,
                        GroupName = raw.GroupName,
                        CompletionState = (int) raw.CompletionState,
                        LastEpisodeNumber = raw.LastEpisodeNumber,
                        Rating = raw.Rating,
                        Votes = raw.Votes,
                        EpisodeRange = string.Join(',', raw.ReleasedEpisodes),
                    }
                ).ToArray();
                RepoFactory.AniDB_GroupStatus.Save(toSave);

                if (maxEpisode > 0)
                {
                    // update the anime with a record of the latest subbed episode
                    anime.LatestEpisodeNumber = maxEpisode;
                    RepoFactory.AniDB_Anime.Save(anime, false);

                    // check if we have this episode in the database
                    // if not get it now by updating the anime record
                    var eps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(AnimeID, maxEpisode);
                    if (eps.Count == 0)
                    {
                        var crAnime = new CommandRequest_GetAnimeHTTP(AnimeID, true, false, false);
                        crAnime.Save();
                    }
                    // update the missing episode stats on groups and children
                    series.QueueUpdateStats();
                }

                if (ServerSettings.Instance.AniDb.DownloadReleaseGroups && response is { Response: { Count: > 0 } })
                    response.Response.DistinctBy(a => a.GroupID).Select(a => new CommandRequest_GetReleaseGroup(a.GroupID, false)).ForEach(a => a.Save());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing CommandRequest_GetReleaseGroupStatus: {AnimeID} - {Ex}", AnimeID, ex);
            }
        }

        private bool ShouldSkip(SVR_AniDB_Anime anime)
        {
            if (ForceRefresh) return false;
            if (!anime.EndDate.HasValue) return false;
            if (anime.EndDate.Value >= DateTime.Now) return false;
            var ts = DateTime.Now - anime.EndDate.Value;
            if (!(ts.TotalDays > 50)) return false;
            // don't skip if we have never downloaded this info before
            var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(AnimeID);
            return grpStatuses is { Count: > 0 };
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetReleaseGroupStatus_{AnimeID}";
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "ForceRefresh"));
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