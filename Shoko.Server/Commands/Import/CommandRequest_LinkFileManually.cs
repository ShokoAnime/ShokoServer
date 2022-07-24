using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.LinkFileManually)]
    public class CommandRequest_LinkFileManually : CommandRequestImplementation
    {
        private new static Logger logger = LogManager.GetCurrentClassLogger();

        public int VideoLocalID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }

        private SVR_AnimeEpisode episode;
        private SVR_VideoLocal vlocal;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

        public override QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null && episode != null)
                    return new QueueStateStruct
                    {
                        queueState = QueueStateEnum.LinkFileManually,
                        extraParams = new[] {vlocal.FileName, episode.Title}
                    };
                return new QueueStateStruct
                {
                    queueState = QueueStateEnum.LinkFileManually,
                    extraParams = new[] {VideoLocalID.ToString(), EpisodeID.ToString()}
                };
            }
        }

        public CommandRequest_LinkFileManually()
        {
        }

        public CommandRequest_LinkFileManually(int vidLocalID, int episodeID)
        {
            VideoLocalID = vidLocalID;
            EpisodeID = episodeID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            CrossRef_File_Episode xref = new CrossRef_File_Episode();
            try
            {
                xref.PopulateManually(vlocal, episode);
                if (Percentage > 0 && Percentage <= 100)
                {
                    xref.Percentage = Percentage;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error populating XREF: {0}", vlocal.ToStringDetailed());
                throw;
            }
            RepoFactory.CrossRef_File_Episode.Save(xref);
            if (ServerSettings.Instance.WebCache.Enabled)
            {
                CommandRequest_WebCacheSendXRefFileEpisode cr =
                    new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                cr.Save();
            }

            if (ServerSettings.Instance.FileQualityFilterEnabled)
            {
                List<SVR_VideoLocal> videoLocals = episode.GetVideoLocals();
                if (videoLocals != null)
                {
                    videoLocals.Sort(FileQualityFilter.CompareTo);
                    List<SVR_VideoLocal> keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                        .ToList();
                    foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                    if (videoLocals.Contains(vlocal)) videoLocals.Remove(vlocal);
                    videoLocals = videoLocals.Where(FileQualityFilter.CheckFileKeep).ToList();

                    foreach (SVR_VideoLocal toDelete in videoLocals)
                    {
                        toDelete.Places.ForEach(a => a.RemoveAndDeleteFile());
                    }
                }
            }

            vlocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

            SVR_AnimeSeries ser = episode.GetAnimeSeries();
            ser.EpisodeAddedDate = DateTime.Now;
            RepoFactory.AnimeSeries.Save(ser, false, true);

            //Update will re-save
            ser.QueueUpdateStats();


            foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
            {
                grp.EpisodeAddedDate = DateTime.Now;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            
            ShokoEventHandler.Instance.OnFileMatched(vlocal.GetBestVideoLocalPlace());

            if (ServerSettings.Instance.AniDb.MyList_AddFiles)
            {
                CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(vlocal.Hash);
                cmdAddFile.Save();
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_LinkFileManually_{VideoLocalID}_{EpisodeID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "VideoLocalID"));
                EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "EpisodeID"));
                Percentage = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "Percentage"));
                vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
                if (null==vlocal)
                {
                    logger.Info("videolocal object {0} not found", VideoLocalID);
                    return false;
                }
                episode = RepoFactory.AnimeEpisode.GetByID(EpisodeID);
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
