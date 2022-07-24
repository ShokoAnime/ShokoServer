using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB
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

        public int RelDepth { get; set; }

        public bool CreateSeriesEntry { get; set; }

        public CommandRequest_GetAnimeHTTP()
        {
        }

        public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations, bool createSeriesEntry, int relDepth = 0)
        {
            AnimeID = animeid;
            DownloadRelations = downloadRelations;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;
            if (RepoFactory.AniDB_Anime.GetByAnimeID(animeid) == null) Priority = (int) CommandRequestPriority.Priority1;
            RelDepth = relDepth;
            CreateSeriesEntry = createSeriesEntry;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

            try
            {
                SVR_AniDB_Anime anime =
                    ShokoService.AniDBProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations, RelDepth, CreateSeriesEntry);

                // NOTE - related anime are downloaded when the relations are created

                // download group status info for this anime
                // the group status will also help us determine missing episodes for a series


                // download reviews
                if (ServerSettings.Instance.AniDb.DownloadReviews)
                {
                    CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(AnimeID, ForceRefresh);
                    cmd.Save();
                }

                // Request an image download
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_GetAnimeHTTP: {AnimeID} - {Ex}", AnimeID, ex);
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
                AnimeID = int.Parse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(AnimeID)));
                if (RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID) == null) Priority = (int) CommandRequestPriority.Priority1;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(DownloadRelations)), out var dlRelations))
                    DownloadRelations = dlRelations;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(ForceRefresh)), out var force))
                    ForceRefresh = force;
                if (int.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(RelDepth)), out var depth))
                    RelDepth = depth;
                if (bool.TryParse(TryGetProperty(docCreator, nameof(CommandRequest_GetAnimeHTTP), nameof(CreateSeriesEntry)), out var create))
                    CreateSeriesEntry = create;
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