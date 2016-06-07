using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetAnimeHTTP : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetAnimeHTTP()
        {
        }

        public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations)
        {
            AnimeID = animeid;
            DownloadRelations = downloadRelations;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_GetAnimeHTTP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }
        public bool DownloadRelations { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority2; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_AnimeInfo, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations);

                // NOTE - related anime are downloaded when the relations are created

                // download group status info for this anime
                // the group status will also help us determine missing episodes for a series


                // download reviews
                if (ServerSettings.AniDB_DownloadReviews)
                {
                    var cmd = new CommandRequest_GetReviews(AnimeID, ForceRefresh);
                    cmd.Save();
                }

                // Request an image download
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetAnimeHTTP: {0} - {1}", AnimeID, ex.ToString());
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "AnimeID"));
                DownloadRelations =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "DownloadRelations"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetAnimeHTTP_{0}", AnimeID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}