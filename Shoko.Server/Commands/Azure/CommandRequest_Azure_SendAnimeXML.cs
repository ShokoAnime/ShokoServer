using System;
using System.IO;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_Azure_SendAnimeXML : CommandRequest
    {
        public virtual int AnimeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeAzure,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_Azure_SendAnimeXML()
        {
        }

        public CommandRequest_Azure_SendAnimeXML(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int) CommandRequestType.Azure_SendAnimeXML;
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

                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                string filePath = ServerSettings.AnimeXmlDirectory;

                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);

                string fileName = $"AnimeDoc_{AnimeID}.xml";
                string fileNameWithPath = Path.Combine(filePath, fileName);

                string rawXML = string.Empty;
                if (File.Exists(fileNameWithPath))
                {
                    StreamReader re = File.OpenText(fileNameWithPath);
                    rawXML = re.ReadToEnd();
                    re.Close();
                }

                Azure_AnimeXML xml = new Azure_AnimeXML
                {
                    AnimeID = AnimeID,
                    AnimeName = anime.MainTitle,
                    DateDownloaded = 0,
                    Username = ServerSettings.AniDB_Username,
                    XMLContent = rawXML
                };
                AzureWebAPI.Send_AnimeXML(xml);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeXML: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Azure_SendAnimeXML_{AnimeID}";
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeXML", "AnimeID"));
            }

            return true;
        }
    }
}