using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeXML : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority10; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.SendAnimeAzure,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_Azure_SendAnimeXML()
        {
        }

        public CommandRequest_Azure_SendAnimeXML(int animeID)
        {
            this.AnimeID = animeID;
            this.CommandType = (int) CommandRequestType.Azure_SendAnimeXML;
            this.Priority = (int) DefaultPriority;

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

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                string filePath = ServerSettings.AnimeXmlDirectory;

                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);

                string fileName = string.Format("AnimeDoc_{0}.xml", AnimeID);
                string fileNameWithPath = Path.Combine(filePath, fileName);

                string rawXML = "";
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
                logger.Error("Error processing CommandRequest_Azure_SendAnimeXML: {0} - {1}", AnimeID, ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_Azure_SendAnimeXML_{0}", this.AnimeID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeXML", "AnimeID"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}