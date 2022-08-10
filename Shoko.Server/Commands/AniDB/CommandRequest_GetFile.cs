using System;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        [XmlIgnore]
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
            Logger.LogInformation("Get AniDB file info: {VideoLocalID}", VideoLocalID);
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            if (handler.IsBanned) throw new AniDBBannedException { BanType = UpdateType.UDPBan, BanExpires = handler.BanTime?.AddHours(handler.BanTimerResetLength) };
            vlocal ??= RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (vlocal == null) return;
            lock (vlocal)
            {
                var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);
                
                UDPResponse<ResponseGetFile> response = null;
                if (aniFile == null || ForceAniDB)
                {
                    var request = new RequestGetFile { Hash = vlocal.Hash, Size = vlocal.FileSize };
                    response = request.Execute(handler);
                }

                if (response?.Response == null)
                {
                    Logger.LogInformation("File {VideoLocalID} ({Ed2kHash} | {FileName}) could not be found on AniDB", vlocal.VideoLocalID, vlocal.ED2KHash, vlocal.GetBestVideoLocalPlace()?.FileName);
                    return;
                }
                // save to the database
                aniFile ??= new SVR_AniDB_File();
                aniFile.Hash = vlocal.Hash;
                aniFile.FileSize = vlocal.FileSize;
                aniFile.AnimeID = response.Response.AnimeID;

                aniFile.DateTimeUpdated = DateTime.Now;
                aniFile.File_Description = response.Response.Description;
                aniFile.File_Source = response.Response.Source.ToString();
                aniFile.FileID = response.Response.FileID;
                aniFile.FileName = response.Response.Filename;
                aniFile.GroupID = response.Response.GroupID ?? 0;

                aniFile.FileVersion = response.Response.Version;
                aniFile.IsCensored = response.Response.Censored;
                aniFile.IsDeprecated = response.Response.Deprecated;
                aniFile.IsChaptered = response.Response.Chaptered;
                aniFile.InternalVersion = 3;

                RepoFactory.AniDB_File.Save(aniFile, false);
                aniFile.CreateLanguages(response.Response);
                aniFile.CreateCrossEpisodes(vlocal.FileName, response.Response);

                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniFile.AnimeID);
                if (anime != null) RepoFactory.AniDB_Anime.Save(anime);
                var series = RepoFactory.AnimeSeries.GetByAnimeID(aniFile.AnimeID);
                series?.UpdateStats(true, true, true);
                Result = RepoFactory.AniDB_File.GetByFileID(aniFile.FileID);
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
