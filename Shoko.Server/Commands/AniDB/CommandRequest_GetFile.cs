using System;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetFileUDP)]
    public class CommandRequest_GetFile : CommandRequestImplementation
    {
        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        private SVR_VideoLocal vlocal;
        public SVR_AniDB_File Result;

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Get AniDB file info: {VideoLocalID}", VideoLocalID);
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            if (handler.IsBanned) throw new AniDBBannedException { BanType = UpdateType.UDPBan, BanExpires = handler.BanTime?.AddHours(handler.BanTimerResetLength) };
            vlocal ??= RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (vlocal == null) return;
            lock (vlocal)
            {
                var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);
                
                UDPResponse<ResponseGetFile> response = null;
                if (aniFile == null || aniFile.FileSize != vlocal.FileSize || ForceAniDB)
                {
                    var request = new RequestGetFile { Hash = vlocal.Hash, Size = vlocal.FileSize };
                    response = request.Execute(handler);
                }

                if (response != null)
                {
                    // save to the database
                    aniFile ??= new SVR_AniDB_File();

                    SVR_AniDB_File.Populate(aniFile, response.Response);

                    RepoFactory.AniDB_File.Save(aniFile, false);
                    aniFile.CreateLanguages(response.Response);
                    aniFile.CreateCrossEpisodes(vlocal.FileName, response.Response);

                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniFile.AnimeID);
                    if (anime != null) RepoFactory.AniDB_Anime.Save(anime);
                    SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(aniFile.AnimeID);
                    series.UpdateStats(true, true, true);
                    Result = RepoFactory.AniDB_File.GetByFileID(aniFile.FileID);
                }
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

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length <= 0) return true;
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", nameof(VideoLocalID)));
            ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetFile", nameof(ForceAniDB)));
            vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now,
            };
            return cq;
        }
    }
}
