using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Commands.AniDB;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetFile : CommandRequestImplementation, ICommandRequest
    {
        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        private VideoLocal vlocal = null;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority3; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null)
                    return new QueueStateStruct() { queueState = QueueStateEnum.GetFileInfo, extraParams = new string[] { vlocal.FullServerPath } };
                else
                    return new QueueStateStruct() { queueState = QueueStateEnum.GetFileInfo, extraParams = new string[] { VideoLocalID.ToString() } };
            }
        }

        public CommandRequest_GetFile()
        {
        }

        public CommandRequest_GetFile(int vidLocalID, bool forceAniDB)
        {
            this.VideoLocalID = vidLocalID;
            this.ForceAniDB = forceAniDB;
            this.CommandType = (int) CommandRequestType.AniDB_GetFileUDP;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Get AniDB file info: {0}", VideoLocalID);


            try
            {
                AniDB_FileRepository repAniFile = new AniDB_FileRepository();
                VideoLocalRepository repVids = new VideoLocalRepository();
                vlocal = repVids.GetByID(VideoLocalID);
                if (vlocal == null) return;

                AniDB_File aniFile = repAniFile.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);

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
                    string localFileName = Path.GetFileName(vlocal.FilePath);
                    aniFile.FileName = localFileName;

                    repAniFile.Save(aniFile, false);
                    aniFile.CreateLanguages();
                    aniFile.CreateCrossEpisodes(localFileName);

                    if (!string.IsNullOrEmpty(fileInfo.OtherEpisodesRAW))
                    {
                        string[] epIDs = fileInfo.OtherEpisodesRAW.Split(',');
                        foreach (string epid in epIDs)
                        {
                            int id = 0;
                            if (int.TryParse(epid, out id))
                            {
                                CommandRequest_GetEpisode cmdEp = new CommandRequest_GetEpisode(id);
                                cmdEp.Save();
                            }
                        }
                    }
                    AnimeSeriesRepository repo = new AnimeSeriesRepository();
                    AniDB_AnimeRepository animerepo = new AniDB_AnimeRepository();
                    AniDB_Anime anime = animerepo.GetByAnimeID(aniFile.AnimeID);
                    if (anime != null)
                    {
                        using (var session = JMMService.SessionFactory.OpenSession())
                        {
                            anime.UpdateContractDetailed(session.Wrap());
                        }
                    }
                    AnimeSeries series = repo.GetByAnimeID(aniFile.AnimeID);
                    series.UpdateStats(false, true, true);
//					StatsCache.Instance.UpdateUsingAniDBFile(vlocal.Hash);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetFile: {0} - {1}", VideoLocalID, ex.ToString());
                return;
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_GetFile_{0}", this.VideoLocalID);
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
                this.VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "VideoLocalID"));
                this.ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "ForceAniDB"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}