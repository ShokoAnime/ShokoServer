using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.GZip;
using JMMServer.Commands.Azure;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetAniDBTitles : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetAniDBTitles()
        {
            CommandType = (int)CommandRequestType.AniDB_GetTitles;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority10; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.AniDB_GetTitles);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetAniDBTitles");


            try
            {
                var process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                var url = Constants.AniDBTitlesURL;
                logger.Trace("Get AniDB Titles: {0}", url);

                var s = Utils.DownloadWebBinary(url);
                var bytes = 2048;
                var data = new byte[2048];
                var b = new StringBuilder();
                var enc = new UTF8Encoding();

                var zis = new GZipInputStream(s);

                while ((bytes = zis.Read(data, 0, data.Length)) > 0)
                    b.Append(enc.GetString(data, 0, bytes));

                zis.Close();

                var repTitles = new AniDB_Anime_TitleRepository();

                var lines = b.ToString().Split('\n');
                var titles = new Dictionary<int, AnimeIDTitle>();
                foreach (var line in lines)
                {
                    if (line.Trim().Length == 0 || line.Trim().Substring(0, 1) == "#") continue;

                    var fields = line.Split('|');

                    var animeID = 0;
                    int.TryParse(fields[0], out animeID);
                    if (animeID == 0) continue;

                    var titleType = fields[1].Trim().ToLower();
                    //string language = fields[2].Trim().ToLower();
                    var titleValue = fields[3].Trim();


                    AnimeIDTitle thisTitle = null;
                    if (titles.ContainsKey(animeID))
                    {
                        thisTitle = titles[animeID];
                    }
                    else
                    {
                        thisTitle = new AnimeIDTitle();
                        thisTitle.AnimeIDTitleId = 0;
                        thisTitle.MainTitle = titleValue;
                        thisTitle.AnimeID = animeID;
                        titles[animeID] = thisTitle;
                    }

                    if (!string.IsNullOrEmpty(thisTitle.Titles))
                        thisTitle.Titles += "|";

                    if (titleType.Equals("1"))
                        thisTitle.MainTitle = titleValue;

                    thisTitle.Titles += titleValue;
                }

                foreach (var aniTitle in titles.Values)
                {
                    //AzureWebAPI.Send_AnimeTitle(aniTitle);
                    var cmdAzure = new CommandRequest_Azure_SendAnimeTitle(aniTitle.AnimeID, aniTitle.MainTitle,
                        aniTitle.Titles);
                    cmdAzure.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetAniDBTitles: {0}", ex.ToString());
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
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetAniDBTitles_{0}", DateTime.Now);
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