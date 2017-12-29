using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ICSharpCode.SharpZipLib.GZip;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Azure;
using Shoko.Models.Queue;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetAniDBTitles : CommandRequest_AniDBBase
    {
        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_GetTitles,
            extraParams = new string[0]
        };

        public CommandRequest_GetAniDBTitles()
        {
            CommandType = (int) CommandRequestType.AniDB_GetTitles;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetAniDBTitles");


            try
            {
                bool process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                string url = Constants.AniDBTitlesURL;
                logger.Trace("Get AniDB Titles: {0}", url);

                Stream s = Misc.DownloadWebBinary(url);
                int bytes = 2048;
                byte[] data = new byte[bytes]; //USE OF BYTES LENGTH VALUES FOR DATA SIZE
                StringBuilder b = new StringBuilder();
                UTF8Encoding enc = new UTF8Encoding();

                GZipInputStream zis = new GZipInputStream(s);

                while ((bytes = zis.Read(data, 0, data.Length)) > 0)
                    b.Append(enc.GetString(data, 0, bytes));

                zis.Close();


                string[] lines = b.ToString().Split('\n');
                Dictionary<int, Azure_AnimeIDTitle> titles = new Dictionary<int, Azure_AnimeIDTitle>();
                foreach (string line in lines)
                {
                    if (line.Trim().Length == 0 || line.Trim().Substring(0, 1) == "#") continue;

                    string[] fields = line.Split('|');

                    int.TryParse(fields[0], out int animeID);
                    if (animeID == 0) continue;

                    string titleType = fields[1].Trim().ToLower();
                    //string language = fields[2].Trim().ToLower();
                    string titleValue = fields[3].Trim();


                    Azure_AnimeIDTitle thisTitle = null;
                    if (titles.ContainsKey(animeID))
                    {
                        thisTitle = titles[animeID];
                    }
                    else
                    {
                        thisTitle = new Azure_AnimeIDTitle
                        {
                            AnimeIDTitleId = 0,
                            MainTitle = titleValue,
                            AnimeID = animeID
                        };
                        titles[animeID] = thisTitle;
                    }

                    if (!string.IsNullOrEmpty(thisTitle.Titles))
                        thisTitle.Titles += "|";

                    if (titleType.Equals("1"))
                        thisTitle.MainTitle = titleValue;

                    thisTitle.Titles += titleValue;
                }

                foreach (Azure_AnimeIDTitle aniTitle in titles.Values)
                {
                    //AzureWebAPI.Send_AnimeTitle(aniTitle);
                    CommandRequest_Azure_SendAnimeTitle cmdAzure =
                        new CommandRequest_Azure_SendAnimeTitle(aniTitle.AnimeID,
                            aniTitle.MainTitle, aniTitle.Titles);
                    cmdAzure.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetAniDBTitles: {0}", ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetAniDBTitles_{DateTime.Now.ToString()}";
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
            }

            return true;
        }
    }
}