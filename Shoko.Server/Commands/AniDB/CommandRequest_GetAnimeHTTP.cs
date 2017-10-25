using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetAnimeHTTP)]
    public class CommandRequest_GetAnimeHTTP : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }
        public bool DownloadRelations { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AnimeInfo,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_GetAnimeHTTP()
        {
        }

        public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations)
        {
            AnimeID = animeid;
            DownloadRelations = downloadRelations;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

            try
            {
                SVR_AniDB_Anime anime =
                    ShokoService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations);

                // NOTE - related anime are downloaded when the relations are created

                // download group status info for this anime
                // the group status will also help us determine missing episodes for a series


                // download reviews
                if (ServerSettings.AniDB_DownloadReviews)
                {
                    CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(AnimeID, ForceRefresh);
                    cmd.Save();
                }

                // Request an image download
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetAnimeHTTP: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetAnimeHTTP_{AnimeID}";
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "AnimeID"));
                DownloadRelations =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "DownloadRelations"));
                ForceRefresh = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "ForceRefresh"));
            }

            return true;
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