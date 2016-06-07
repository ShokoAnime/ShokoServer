using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Commands.AniDB;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetFile : CommandRequestImplementation, ICommandRequest
    {
        private VideoLocal vlocal;

        public CommandRequest_GetFile()
        {
        }

        public CommandRequest_GetFile(int vidLocalID, bool forceAniDB)
        {
            VideoLocalID = vidLocalID;
            ForceAniDB = forceAniDB;
            CommandType = (int)CommandRequestType.AniDB_GetFileUDP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority3; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                if (vlocal != null)
                    return string.Format(Resources.Command_GetFileInfo, vlocal.FullServerPath);
                return string.Format(Resources.Command_GetFileInfo, VideoLocalID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Get AniDB file info: {0}", VideoLocalID);


            try
            {
                var repAniFile = new AniDB_FileRepository();
                var repVids = new VideoLocalRepository();
                vlocal = repVids.GetByID(VideoLocalID);
                if (vlocal == null) return;

                var aniFile = repAniFile.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);

                /*// get anidb file info from web cache
				if (aniFile == null && ServerSettings.WebCache_AniDB_File_Get)
				{
					AniDB_FileRequest fr = XMLService.Get_AniDB_File(vlocal.Hash, vlocal.FileSize);
					if (fr != null)
					{
						aniFile = new AniDB_File();
						aniFile.Populate(fr);

						//overwrite with local file name
						string localFileName = Path.GetFileName(vlocal.FilePath);
						aniFile.FileName = localFileName;

						repAniFile.Save(aniFile, false);
						aniFile.CreateLanguages();
						aniFile.CreateCrossEpisodes(localFileName);

						StatsCache.Instance.UpdateUsingAniDBFile(vlocal.Hash);
					}
				}*/

                Raw_AniDB_File fileInfo = null;
                if (aniFile == null || ForceAniDB)
                    fileInfo = JMMService.AnidbProcessor.GetFileInfo(vlocal);

                if (fileInfo != null)
                {
                    // save to the database
                    if (aniFile == null)
                        aniFile = new AniDB_File();

                    aniFile.Populate(fileInfo);

                    //overwrite with local file name
                    var localFileName = Path.GetFileName(vlocal.FilePath);
                    aniFile.FileName = localFileName;

                    repAniFile.Save(aniFile, false);
                    aniFile.CreateLanguages();
                    aniFile.CreateCrossEpisodes(localFileName);

                    if (!string.IsNullOrEmpty(fileInfo.OtherEpisodesRAW))
                    {
                        var epIDs = fileInfo.OtherEpisodesRAW.Split(',');
                        foreach (var epid in epIDs)
                        {
                            var id = 0;
                            if (int.TryParse(epid, out id))
                            {
                                var cmdEp = new CommandRequest_GetEpisode(id);
                                cmdEp.Save();
                            }
                        }
                    }

                    StatsCache.Instance.UpdateUsingAniDBFile(vlocal.Hash);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetFile: {0} - {1}", VideoLocalID, ex.ToString());
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
                VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "VideoLocalID"));
                ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "ForceAniDB"));
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetFile_{0}", VideoLocalID);
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