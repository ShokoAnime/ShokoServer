using System;
using System.IO;
using System.Xml;
using AniDBAPI;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetFile : CommandRequest_AniDBBase
    {
        public virtual int VideoLocalID { get; set; }
        public virtual bool ForceAniDB { get; set; }

        private SVR_VideoLocal vlocal;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

        public override QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null)
                    return new QueueStateStruct
                    {
                        queueState = QueueStateEnum.GetFileInfo,
                        extraParams = new[] {vlocal.FileName}
                    };
                return new QueueStateStruct
                {
                    queueState = QueueStateEnum.GetFileInfo,
                    extraParams = new[] {VideoLocalID.ToString()}
                };
            }
        }

        public CommandRequest_GetFile()
        {
        }

        public CommandRequest_GetFile(int vidLocalID, bool forceAniDB)
        {
            VideoLocalID = vidLocalID;
            ForceAniDB = forceAniDB;
            CommandType = (int) CommandRequestType.AniDB_GetFileUDP;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Get AniDB file info: {0}", VideoLocalID);


            try
            {
                if (vlocal == null)
                    vlocal = Repo.VideoLocal.GetByID(VideoLocalID);
                if (vlocal == null) return;
                SVR_AniDB_File aniFile = Repo.AniDB_File.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);

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

                string localFileName = vlocal.FileName;
                if (aniFile == null || ForceAniDB)
                {
                    Raw_AniDB_File fileInfo = ShokoService.AnidbProcessor.GetFileInfo(vlocal);

                    if (fileInfo != null)
                    {
                        using (var upd = Repo.AniDB_File.BeginUpdate(aniFile))
                        {
                            upd.Entity.Populate_RA(fileInfo);
                            upd.Entity.FileName = localFileName;
                            aniFile = upd.Commit();
                        }

                        // save to the database
                        aniFile.CreateLanguages();
                        aniFile.CreateCrossEpisodes(localFileName);

                        if (!string.IsNullOrEmpty(fileInfo.OtherEpisodesRAW))
                        {
                            string[] epIDs = fileInfo.OtherEpisodesRAW.Split(',');
                            foreach (string epid in epIDs)
                            {
                                if (int.TryParse(epid, out int id))
                                {
                                    CommandRequest_GetEpisode cmdEp = new CommandRequest_GetEpisode(id);
                                    cmdEp.Save();
                                }
                            }
                        }

                        SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(aniFile.AnimeID);
                        if (anime != null) Repo.AniDB_Anime.BeginUpdate(anime).Commit();
                        SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(aniFile.AnimeID);
                        series.UpdateStats(false, true, true);
                    }
//                  StatsCache.Instance.UpdateUsingAniDBFile(vlocal.Hash);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetFile: {0} - {1}", VideoLocalID, ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetFile_{VideoLocalID}";
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
                VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "VideoLocalID"));
                ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", "ForceAniDB"));
                vlocal = Repo.VideoLocal.GetByID(VideoLocalID);
            }

            return true;
        }
    }
}