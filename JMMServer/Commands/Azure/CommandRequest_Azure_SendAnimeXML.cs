using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeXML : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_Azure_SendAnimeXML()
        {
        }

        public CommandRequest_Azure_SendAnimeXML(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int)CommandRequestType.Azure_SendAnimeXML;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority10; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SendAnimeAzure, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                var rep = new AniDB_AnimeRepository();
                var anime = rep.GetByAnimeID(AnimeID);
                if (anime == null) return;

                var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var filePath = Path.Combine(appPath, "Anime_HTTP");

                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);

                var fileName = string.Format("AnimeDoc_{0}.xml", AnimeID);
                var fileNameWithPath = Path.Combine(filePath, fileName);

                var rawXML = "";
                if (File.Exists(fileNameWithPath))
                {
                    var re = File.OpenText(fileNameWithPath);
                    rawXML = re.ReadToEnd();
                    re.Close();
                }

                var xml = new AnimeXML();
                xml.AnimeID = AnimeID;
                xml.AnimeName = anime.MainTitle;
                xml.DateDownloaded = 0;
                xml.Username = ServerSettings.AniDB_Username;
                xml.XMLContent = rawXML;

                AzureWebAPI.Send_AnimeXML(xml);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeXML: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeXML", "AnimeID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_Azure_SendAnimeXML_{0}", AnimeID);
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