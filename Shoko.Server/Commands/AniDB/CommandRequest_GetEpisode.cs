using System;
using System.Collections.Generic;
using System.Xml;
using AniDBAPI;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetEpisode : CommandRequest_AniDBBase
    {
        public virtual int EpisodeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetEpisodeList,
            extraParams = new[] {EpisodeID.ToString()}
        };

        public CommandRequest_GetEpisode()
        {
        }

        public CommandRequest_GetEpisode(int epID)
        {
            EpisodeID = epID;
            CommandType = (int) CommandRequestType.AniDB_GetEpisodeUDP;
            Priority = (int) DefaultPriority;
            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Get AniDB episode info: {0}", EpisodeID);


            try
            {
                // we don't use this command to update episode info
                // we actually use it to update the cross ref info instead
                // and we only use it for the "Other Episodes" section of the FILE command
                // because that field doesn't tell you what anime it belongs to

                List<CrossRef_File_Episode> xrefs = Repo.CrossRef_File_Episode.GetByEpisodeID(EpisodeID);
                if (xrefs.Count == 0) return;

                Raw_AniDB_Episode epInfo = ShokoService.AnidbProcessor.GetEpisodeInfo(EpisodeID);

                if (epInfo != null)
                {
                    List<int> oldanimes = new List<int>();
                    using (var upd = Repo.CrossRef_File_Episode.BeginBatchUpdate(() => Repo.CrossRef_File_Episode.GetByEpisodeID(EpisodeID)))
                    {
                        foreach (CrossRef_File_Episode xref in xrefs)
                        {
                            if (xref.AnimeID != epInfo.AnimeID)
                            {
                                if (!oldanimes.Contains(xref.AnimeID))
                                    oldanimes.Add(xref.AnimeID);
                                xref.AnimeID = epInfo.AnimeID;
                                upd.Update(xref);
                            }
                        }

                        upd.Commit();
                    }
                    if (oldanimes.Count > 0)
                    {
                        foreach (int x in oldanimes)
                            Repo.AnimeSeries.GetByAnimeID(x)?.QueueUpdateStats();
                        Repo.AnimeSeries.GetByAnimeID(epInfo.AnimeID)?.QueueUpdateStats();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetEpisode: {0} - {1}", EpisodeID, ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetEpisode_{EpisodeID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetEpisode", "EpisodeID"));
            }

            return true;
        }
    }
}