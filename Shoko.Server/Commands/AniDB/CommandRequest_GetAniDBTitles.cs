﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Repositories;
using Shoko.Models.Server;
using Shoko.Server.Commands.Azure;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetAniDBTitles : CommandRequestImplementation, ICommandRequest
    {
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
                    queueState = QueueStateEnum.AniDB_GetTitles,
                    extraParams = new string[0]
                };
            }
        }

        public CommandRequest_GetAniDBTitles()
        {
            this.CommandType = (int) CommandRequestType.AniDB_GetTitles;
            this.Priority = (int) DefaultPriority;

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

                Stream s = Utils.DownloadWebBinary(url);
                int bytes = 2048;
                byte[] data = new byte[bytes]; //USE OF BYTES LENGTH VALUES FOR DATA SIZE
                StringBuilder b = new StringBuilder();
                UTF8Encoding enc = new UTF8Encoding();

                ICSharpCode.SharpZipLib.GZip.GZipInputStream zis = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(s);

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
                logger.Error("Error processing CommandRequest_GetAniDBTitles: {0}", ex.ToString());
                return;
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_GetAniDBTitles_{0}", DateTime.Now.ToString());
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