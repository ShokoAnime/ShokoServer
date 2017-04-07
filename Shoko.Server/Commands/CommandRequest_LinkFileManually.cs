using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using NutzCode.CloudFileSystem;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_LinkFileManually : CommandRequestImplementation, ICommandRequest
    {
        public int VideoLocalID { get; set; }
        public int EpisodeID { get; set; }

        private SVR_AnimeEpisode episode = null;
        private SVR_VideoLocal vlocal = null;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null && episode != null)
                    return new QueueStateStruct()
                    {
                        queueState = QueueStateEnum.LinkFileManually,
                        extraParams = new string[] {vlocal.FileName, episode.AniDB_Episode.EnglishName}
                    };
                else
                    return new QueueStateStruct()
                    {
                        queueState = QueueStateEnum.LinkFileManually,
                        extraParams = new string[] {VideoLocalID.ToString(), EpisodeID.ToString()}
                    };
            }
        }

        public CommandRequest_LinkFileManually()
        {
        }

        public CommandRequest_LinkFileManually(int vidLocalID, int episodeID)
        {
            this.VideoLocalID = vidLocalID;
            this.EpisodeID = episodeID;
            this.CommandType = (int) CommandRequestType.LinkFileManually;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            episode = RepoFactory.AnimeEpisode.GetByID(EpisodeID);
            CrossRef_File_Episode xref = new CrossRef_File_Episode();
            try
            {
                xref.PopulateManually(vlocal, episode);
            }
            catch (Exception ex)
            {
                string msg = string.Format("Error populating XREF: {0}", vlocal.ToStringDetailed());
                throw;
            }
            RepoFactory.CrossRef_File_Episode.Save(xref);
            CommandRequest_WebCacheSendXRefFileEpisode cr =
                new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
            cr.Save();
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

            CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(vlocal.Hash);
            cmdAddFile.Save();
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_LinkFileManually_{VideoLocalID}_{EpisodeID}";
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
                this.VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "VideoLocalID"));
                this.EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "EpisodeID"));
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