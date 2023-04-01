using System;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetFileUDP)]
public class CommandRequest_GetFile : CommandRequestImplementation
{
    private readonly IUDPConnectionHandler _handler;
    private readonly IRequestFactory _requestFactory;

    public int VideoLocalID { get; set; }
    public bool ForceAniDB { get; set; }

    private SVR_VideoLocal vlocal;
    [XmlIgnore] public SVR_AniDB_File Result;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (vlocal != null)
            {
                return new QueueStateStruct
                {
                    message = "Getting file info from UDP API: {0}",
                    queueState = QueueStateEnum.GetFileInfo,
                    extraParams = new[] { vlocal.FileName }
                };
            }

            return new QueueStateStruct
            {
                message = "Getting file info from UDP API: {0}",
                queueState = QueueStateEnum.GetFileInfo,
                extraParams = new[] { VideoLocalID.ToString() }
            };
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Get AniDB file info: {VideoLocalID}", VideoLocalID);

        if (_handler.IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.UDPBan, BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
            };
        }

        vlocal ??= RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (vlocal == null)
        {
            return;
        }

        lock (vlocal)
        {
            var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vlocal.Hash, vlocal.FileSize);

            UDPResponse<ResponseGetFile> response = null;
            if (aniFile == null || ForceAniDB)
            {
                var request = _requestFactory.Create<RequestGetFile>(
                    r =>
                    {
                        r.Hash = vlocal.Hash;
                        r.Size = vlocal.FileSize;
                    }
                );
                response = request.Execute();
            }

            if (response?.Response == null)
            {
                Logger.LogInformation("File {VideoLocalID} ({Ed2kHash} | {FileName}) could not be found on AniDB",
                    vlocal.VideoLocalID, vlocal.ED2KHash, vlocal.GetBestVideoLocalPlace()?.FileName);
                return;
            }

            // save to the database
            aniFile ??= new SVR_AniDB_File();
            aniFile.Hash = vlocal.Hash;
            aniFile.FileSize = vlocal.FileSize;

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
            CreateLanguages(response.Response);
            CreateEpisodes(vlocal.FileName, response.Response);

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(response.Response.AnimeID);
            if (anime != null)
            {
                RepoFactory.AniDB_Anime.Save(anime, false);
            }

            var series = RepoFactory.AnimeSeries.GetByAnimeID(response.Response.AnimeID);
            series?.UpdateStats(true, true);
            series?.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
            Result = RepoFactory.AniDB_File.GetByFileID(aniFile.FileID);
        }
    }

    public void CreateLanguages(ResponseGetFile response)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        using var trans = session.BeginTransaction();
        // Only update languages if we got a response
        if (response?.AudioLanguages is not null)
        {
            // Delete old
            var toDelete = RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(response.FileID);
            RepoFactory.CrossRef_Languages_AniDB_File.DeleteWithOpenTransaction(session, toDelete);

            // Save new
            var toSave = response.AudioLanguages.Select(language => language.Trim().ToLower())
                .Where(lang => lang.Length > 0)
                .Select(lang => new CrossRef_Languages_AniDB_File { LanguageName = lang, FileID = response.FileID })
                .ToList();
            RepoFactory.CrossRef_Languages_AniDB_File.SaveWithOpenTransaction(session, toSave);
        }

        if (response?.SubtitleLanguages is not null)
        {
            // Delete old
            var toDelete = RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(response.FileID);
            RepoFactory.CrossRef_Subtitles_AniDB_File.DeleteWithOpenTransaction(session, toDelete);

            // Save new
            var toSave = response.SubtitleLanguages.Select(language => language.Trim().ToLower())
                .Where(lang => lang.Length > 0)
                .Select(lang => new CrossRef_Subtitles_AniDB_File { LanguageName = lang, FileID = response.FileID })
                .ToList();
            RepoFactory.CrossRef_Subtitles_AniDB_File.SaveWithOpenTransaction(session, toSave);
        }
        trans.Commit();
    }

    public void CreateEpisodes(string filename, ResponseGetFile response)
    {
        if (response.EpisodeIDs.Count <= 0)
        {
            return;
        }

        var fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(vlocal.Hash);

        // Use a single session A. for efficiency and B. to prevent regenerating stats
        
        lock (BaseRepository.GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var trans = session.BeginTransaction();
            RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(session, fileEps);
            trans.Commit();
        }

        fileEps = response.EpisodeIDs
            .Select(
                (ep, x) => new CrossRef_File_Episode
                {
                    Hash = vlocal.Hash,
                    CrossRefSource = (int)CrossRefSource.AniDB,
                    AnimeID = response.AnimeID,
                    EpisodeID = ep.EpisodeID,
                    Percentage = ep.Percentage,
                    EpisodeOrder = x + 1,
                    FileName = filename,
                    FileSize = vlocal.FileSize
                }
            )
            .ToList();

        if (response.OtherEpisodes.Count > 0)
        {
            Logger.LogInformation("Found {Count} additional episodes for file", response.OtherEpisodes.Count);
            var epOrder = fileEps.Max(a => a.EpisodeOrder);
            foreach (var episode in response.OtherEpisodes)
            {
                var epAnimeID = RepoFactory.AniDB_Episode.GetByEpisodeID(episode.EpisodeID)?.AnimeID;
                if (epAnimeID == null)
                {
                    Logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, downloading more info", episode.EpisodeID);
                    var epRequest = _requestFactory.Create<RequestGetEpisode>(r => r.EpisodeID = episode.EpisodeID);
                    try
                    {
                        var epResponse = epRequest.Execute();
                        epAnimeID = epResponse.Response?.AnimeID;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Could not get Episode Info for {EpisodeID}", episode.EpisodeID);
                    }
                }

                if (epAnimeID == null) continue;

                epOrder++;
                fileEps.Add(new CrossRef_File_Episode
                {
                    Hash = vlocal.Hash,
                    CrossRefSource = (int)CrossRefSource.AniDB,
                    AnimeID = epAnimeID.Value,
                    EpisodeID = episode.EpisodeID,
                    Percentage = episode.Percentage,
                    EpisodeOrder = epOrder,
                    FileName = filename,
                    FileSize = vlocal.FileSize
                });
            }
        }


        // There is a chance that AniDB returned a dup, however unlikely
        lock (BaseRepository.GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var trans = session.BeginTransaction();
            RepoFactory.CrossRef_File_Episode.SaveWithOpenTransaction(session,
                fileEps.DistinctBy(a => $"{a.Hash}-{a.EpisodeID}").ToList());
            trans.Commit();
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
        if (CommandDetails.Trim().Length <= 0) return false;

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
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_GetFile(ILoggerFactory loggerFactory, IUDPConnectionHandler handler,
        IRequestFactory requestFactory) : base(loggerFactory)
    {
        _handler = handler;
        _requestFactory = requestFactory;
    }

    protected CommandRequest_GetFile()
    {
    }
}
