using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired, NetworkRequired, AniDBUDPRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
[Command(CommandRequestType.AniDB_GetFileUDP)]
public class AniDBGetFileJob : BaseJob
{
    private readonly IUDPConnectionHandler _handler;
    private readonly IRequestFactory _requestFactory;
    private SVR_VideoLocal _vlocal;

    public virtual int VideoLocalID { get; set; }
    public virtual bool ForceAniDB { get; set; }
    [JsonIgnore] public virtual SVR_AniDB_File Result { get; set; }

    public override QueueStateStruct Description
    {
        get
        {
            if (_vlocal != null)
            {
                return new QueueStateStruct
                {
                    message = "Getting file info from UDP API: {0}",
                    queueState = QueueStateEnum.GetFileInfo,
                    extraParams = new[] { _vlocal.FileName }
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

    public override async Task Process()
    {
        Logger.LogInformation("Get AniDB file info: {VideoLocalID}", VideoLocalID);

        if (_handler.IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.UDPBan, BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
            };
        }

        _vlocal ??= RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null)
        {
            return;
        }

        var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(_vlocal.Hash, _vlocal.FileSize);

        UDPResponse<ResponseGetFile> response = null;
        if (aniFile == null || ForceAniDB)
        {
            var request = _requestFactory.Create<RequestGetFile>(
                r =>
                {
                    r.Hash = _vlocal.Hash;
                    r.Size = _vlocal.FileSize;
                }
            );
            response = request.Execute();
        }

        if (response?.Response == null)
        {
            Logger.LogInformation("File {VideoLocalID} ({Ed2kHash} | {FileName}) could not be found on AniDB",
                _vlocal.VideoLocalID, _vlocal.ED2KHash, _vlocal.GetBestVideoLocalPlace()?.FileName);
            return;
        }

        // remap if the hash brought up the wrong file
        var tempAniDBFile = RepoFactory.AniDB_File.GetByFileID(response.Response.FileID);
        if (aniFile != null && tempAniDBFile != null && aniFile != tempAniDBFile)
        {
            RepoFactory.AniDB_File.Delete(aniFile);
            aniFile = tempAniDBFile;
        }

        // save to the database
        aniFile ??= new SVR_AniDB_File();
        aniFile.Hash = _vlocal.Hash;
        aniFile.FileSize = _vlocal.FileSize;

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
        await CreateLanguages(response.Response);
        await CreateEpisodes(_vlocal.FileName, response.Response);

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

    private static async Task CreateLanguages(ResponseGetFile response)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        // playing with async
        await BaseRepository.Lock(session, async s =>
        {
            using var trans = s.BeginTransaction();
            // Only update languages if we got a response
            if (response?.AudioLanguages is not null)
            {
                // Delete old
                var toDelete = RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(response.FileID);
                await RepoFactory.CrossRef_Languages_AniDB_File.DeleteWithOpenTransactionAsync(s, toDelete);

                // Save new
                var toSave = response.AudioLanguages.Select(language => language.Trim().ToLower())
                    .Where(lang => lang.Length > 0)
                    .Select(lang => new CrossRef_Languages_AniDB_File
                    {
                        LanguageName = lang, FileID = response.FileID
                    })
                    .ToList();
                await RepoFactory.CrossRef_Languages_AniDB_File.SaveWithOpenTransactionAsync(s, toSave);
            }

            if (response?.SubtitleLanguages is not null)
            {
                // Delete old
                var toDelete = RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(response.FileID);
                await RepoFactory.CrossRef_Subtitles_AniDB_File.DeleteWithOpenTransactionAsync(s, toDelete);

                // Save new
                var toSave = response.SubtitleLanguages.Select(language => language.Trim().ToLower())
                    .Where(lang => lang.Length > 0)
                    .Select(lang => new CrossRef_Subtitles_AniDB_File
                    {
                        LanguageName = lang, FileID = response.FileID
                    })
                    .ToList();
                await RepoFactory.CrossRef_Subtitles_AniDB_File.SaveWithOpenTransactionAsync(s, toSave);
            }

            await trans.CommitAsync();
        });
    }

    private async Task CreateEpisodes(string filename, ResponseGetFile response)
    {
        if (response.EpisodeIDs.Count <= 0) return;

        var fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(_vlocal.Hash);

        // Use a single session A. for efficiency and B. to prevent regenerating stats

        await BaseRepository.Lock(fileEps, async x =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var trans = session.BeginTransaction();
            await RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransactionAsync(session, x);
            await trans.CommitAsync();
        });

        fileEps = response.EpisodeIDs
            .Select(
                (ep, x) => new CrossRef_File_Episode
                {
                    Hash = _vlocal.Hash,
                    CrossRefSource = (int)CrossRefSource.AniDB,
                    AnimeID = response.AnimeID,
                    EpisodeID = ep.EpisodeID,
                    Percentage = ep.Percentage,
                    EpisodeOrder = x + 1,
                    FileName = filename,
                    FileSize = _vlocal.FileSize
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
                    Hash = _vlocal.Hash,
                    CrossRefSource = (int)CrossRefSource.AniDB,
                    AnimeID = epAnimeID.Value,
                    EpisodeID = episode.EpisodeID,
                    Percentage = episode.Percentage,
                    EpisodeOrder = epOrder,
                    FileName = filename,
                    FileSize = _vlocal.FileSize
                });
            }
        }


        // There is a chance that AniDB returned a dup, however unlikely
        await BaseRepository.Lock(fileEps, async x =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var trans = session.BeginTransaction();
            await RepoFactory.CrossRef_File_Episode.SaveWithOpenTransactionAsync(session, x.DistinctBy(a => (a.Hash, a.EpisodeID)).ToList());
            await trans.CommitAsync();
        });
    }

    public AniDBGetFileJob(ILoggerFactory loggerFactory, IUDPConnectionHandler handler,
        IRequestFactory requestFactory) : base(loggerFactory)
    {
        _handler = handler;
        _requestFactory = requestFactory;
    }

    protected AniDBGetFileJob()
    {
    }
}
