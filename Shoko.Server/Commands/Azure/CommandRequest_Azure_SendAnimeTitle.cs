using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands
{
    public class CommandRequest_Azure_SendAnimeTitle : CommandRequest
    {
        public virtual int AnimeID { get; set; }
        public virtual string MainTitle { get; set; }
        public virtual string Titles { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeTitle,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_Azure_SendAnimeTitle()
        {
        }

        public CommandRequest_Azure_SendAnimeTitle(int animeID, string main, string titles)
        {
            AnimeID = animeID;
            MainTitle = main;
            Titles = titles;
            CommandType = (int) CommandRequestType.Azure_SendAnimeTitle;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                bool process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                Azure_AnimeIDTitle thisTitle = new Azure_AnimeIDTitle
                {
                    AnimeIDTitleId = 0,
                    MainTitle = MainTitle,
                    AnimeID = AnimeID,
                    Titles = Titles
                };
                AzureWebAPI.Send_AnimeTitle(thisTitle);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeTitle: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Azure_SendAnimeTitle_{AnimeID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "AnimeID"));
                MainTitle = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "MainTitle");
                Titles = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "Titles");
            }

            return true;
        }
    }
}