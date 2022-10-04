using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities.MediaInfoLib;
using Media = Shoko.Models.PlexAndKodi.Media;
using MediaContainer = Shoko.Models.MediaInfo.MediaContainer;

namespace Shoko.Server.Models;

public class SVR_VideoLocal : VideoLocal, IHash
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    #region DB columns

    public int MediaVersion { get; set; }
    public byte[] MediaBlob { get; set; }
    public int MediaSize { get; set; }

    #endregion


    public int MyListID { get; set; }

    public bool IsManualLink => GetAniDBFile() == null;

    /// <summary>
    /// Duration in ms. (MediaInfo model has it in seconds
    /// </summary>
    public long Duration => (long) (Media?.GeneralStream?.Duration * 1000 ?? 0);

    /// <summary>
    /// Duration as a TimeSpan
    /// </summary>
    public TimeSpan DurationTimeSpan => new TimeSpan(0, 0, (int)(Media?.GeneralStream?.Duration ?? 0));

    public string VideoResolution => Media?.VideoStream == null ? "0x0" : $"{Media.VideoStream.Width}x{Media.VideoStream.Height}";

    public string Info => string.IsNullOrEmpty(FileName) ? string.Empty : FileName;


    public const int MEDIA_VERSION = 5;


    private MediaContainer _media { get; set; }

    public virtual MediaContainer Media
    {
        get
        {
            if (MediaVersion == MEDIA_VERSION && (_media?.GeneralStream?.Duration ?? 0) == 0 && MediaBlob != null &&
                MediaBlob.Length > 0 && MediaSize > 0)
            {
                try
                {
                    _media = MessagePackSerializer.Deserialize<MediaContainer>(MediaBlob, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to Deserialize MediaContainer as MessagePack: {Ex}", e);
                }
            }
            return _media;
        }
        set
        {
            _media = value;
            MediaBlob = MessagePackSerializer.Serialize(_media, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
            MediaSize = MediaBlob.Length;
            MediaVersion = MEDIA_VERSION;
        }
    }


    public List<SVR_VideoLocal_Place> Places => RepoFactory.VideoLocalPlace.GetByVideoLocal(VideoLocalID);


    public void CollectContractMemory()
    {
        _media = null;
    }

    public string ToStringDetailed()
    {
        StringBuilder sb = new StringBuilder("");
        sb.Append(Environment.NewLine);
        sb.Append("VideoLocalID: " + VideoLocalID);

        sb.Append(Environment.NewLine);
        sb.Append("FileName: " + FileName);
        sb.Append(Environment.NewLine);
        sb.Append("Hash: " + Hash);
        sb.Append(Environment.NewLine);
        sb.Append("FileSize: " + FileSize);
        sb.Append(Environment.NewLine);
        return sb.ToString();
    }

    public string ED2KHash
    {
        get => Hash;
        set => Hash = value;
    }


    public SVR_AniDB_File GetAniDBFile()
    {
        return RepoFactory.AniDB_File.GetByHash(Hash);
    }


    public SVR_VideoLocal_User GetUserRecord(int userID)
    {
        return RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, VideoLocalID);
    }

    public SVR_VideoLocal_User GetOrCreateUserRecord(int userID)
    {
        var userRecord = GetUserRecord(userID);
        if (userRecord != null)
            return userRecord;
        userRecord = new(userID, VideoLocalID);
        RepoFactory.VideoLocalUser.Save(userRecord);
        return userRecord;
    }

    internal AniDB_ReleaseGroup ReleaseGroup
    {
        get
        {
            SVR_AniDB_File anifile = GetAniDBFile();
            if (anifile == null) return null;

            return RepoFactory.AniDB_ReleaseGroup.GetByGroupID(anifile.GroupID);
        }
    }

    public List<SVR_AnimeEpisode> GetAnimeEpisodes()
    {
        return RepoFactory.AnimeEpisode.GetByHash(Hash);
    }


    public List<CrossRef_File_Episode> EpisodeCrossRefs
    {
        get
        {
            if (Hash.Length == 0) return new List<CrossRef_File_Episode>();

            return RepoFactory.CrossRef_File_Episode.GetByHash(Hash);
        }
    }

    private void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
    {
        SVR_VideoLocal_User vidUserRecord = GetUserRecord(userID);
        if (watched)
        {
            if (vidUserRecord == null)
                vidUserRecord = new(userID, VideoLocalID);
            vidUserRecord.WatchedDate = DateTime.Now;
            vidUserRecord.WatchedCount++;

            if (watchedDate.HasValue && updateWatchedDate)
                vidUserRecord.WatchedDate = watchedDate.Value;

            vidUserRecord.LastUpdated = DateTime.Now;
            RepoFactory.VideoLocalUser.Save(vidUserRecord);
        }
        else
        {
            if (vidUserRecord != null)
            {
                vidUserRecord.WatchedDate = null;
                RepoFactory.VideoLocalUser.Save(vidUserRecord);
            }
        }
    }

    public static bool ResolveFile(string fullname)
    {
        if (string.IsNullOrEmpty(fullname)) return false;
        Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(fullname);
        if (tup.Item1 == null)
            return false;
        try
        {
            return File.Exists(fullname);
        }
        catch (Exception)
        {
            logger.Warn("File with Exception: " + fullname);
            return false;
        }
    }

    public FileInfo GetBestFileLink()
    {
        foreach (SVR_VideoLocal_Place p in Places.OrderBy(a => a.ImportFolderType))
        {
            if (ResolveFile(p.FullServerPath))
                return new FileInfo(p.FullServerPath);
        }
        return null;
    }

    public SVR_VideoLocal_Place GetBestVideoLocalPlace(bool resolve = false)
    {
        if (!resolve)
            return Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath)).OrderBy(a => a.ImportFolderType)
                .FirstOrDefault();

        return Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath)).OrderBy(a => a.ImportFolderType)
            .FirstOrDefault(p => ResolveFile(p.FullServerPath) != null);
    }

    public void SetResumePosition(long resumeposition, int userID)
    {
        SVR_VideoLocal_User userRecord = GetOrCreateUserRecord(userID);
        userRecord.ResumePosition = resumeposition;
        userRecord.LastUpdated = DateTime.Now;
        RepoFactory.VideoLocalUser.Save(userRecord);
    }

    public void ToggleWatchedStatus(bool watched, int userID)
    {
        ToggleWatchedStatus(watched, true, watched ? DateTime.Now : null, true, userID, true, true);
    }

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID,
        bool syncTrakt, bool updateWatchedDate)
    {
        var commandFactory = ShokoServer.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null) return;

        List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();

        // update the video file to watched
        int mywatched = watched ? 1 : 0;

        if (user.IsAniDBUser == 0)
            SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate);
        else
            foreach (SVR_JMMUser juser in aniDBUsers)
                if (juser.IsAniDBUser == 1)
                    SaveWatchedStatus(watched, juser.JMMUserID, watchedDate, updateWatchedDate);


        // now lets find all the associated AniDB_File record if there is one
        if (user.IsAniDBUser == 1)
        {
            if (updateOnline)
                if ((watched && ServerSettings.Instance.AniDb.MyList_SetWatched) ||
                    (!watched && ServerSettings.Instance.AniDb.MyList_SetUnwatched))
                {
                    var cmd = commandFactory.Create<CommandRequest_UpdateMyListFileStatus>(
                        c =>
                        {
                            c.Hash = Hash;
                            c.Watched = watched;
                            c.UpdateSeriesStats = false;
                            c.WatchedDateAsSecs = AniDB.GetAniDBDateAsSeconds(watchedDate?.ToUniversalTime());
                        }
                    );
                    cmd.Save();
                }
        }

        // now find all the episode records associated with this video file
        // but we also need to check if theer are any other files attached to this episode with a watched
        // status, 


        SVR_AnimeSeries ser = null;
        // get all files associated with this episode
        List<CrossRef_File_Episode> xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(Hash);
        Dictionary<int, SVR_AnimeSeries> toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
        if (watched)
        {
            // find the total watched percentage
            // eg one file can have a % = 100
            // or if 2 files make up one episodes they will each have a % = 50

            foreach (CrossRef_File_Episode xref in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                int epPercentWatched = 0;
                foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                {
                    SVR_VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                    if (vidUser?.WatchedDate != null)
                        epPercentWatched += filexref.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched > 95)
                {
                    ser = ep.GetAnimeSeries();
                    // a problem
                    if (ser == null) continue;
                    if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                        toUpdateSeries.Add(ser.AnimeSeriesID, ser);
                    if (user.IsAniDBUser == 0)
                        ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                    else
                        foreach (SVR_JMMUser juser in aniDBUsers)
                            if (juser.IsAniDBUser == 1)
                                ep.SaveWatchedStatus(true, juser.JMMUserID, watchedDate, updateWatchedDate);

                    if (syncTrakt && ServerSettings.Instance.TraktTv.Enabled &&
                        !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    {
                        var cmdSyncTrakt = commandFactory.Create<CommandRequest_TraktHistoryEpisode>(
                            c =>
                            {
                                c.AnimeEpisodeID = ep.AnimeEpisodeID;
                                c.Action = (int)TraktSyncAction.Add;
                            }
                        );
                        cmdSyncTrakt.Save();
                    }
                }
            }
        }
        else
        {
            // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
            foreach (CrossRef_File_Episode xrefEp in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                int epPercentWatched = 0;
                foreach (CrossRef_File_Episode filexref in ep.FileCrossRefs)
                {
                    SVR_VideoLocal_User vidUser = filexref.GetVideoLocalUserRecord(userID);
                    if (vidUser?.WatchedDate != null)
                        epPercentWatched += filexref.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched < 95)
                {
                    if (user.IsAniDBUser == 0)
                        ep.SaveWatchedStatus(false, userID, watchedDate, true);
                    else
                        foreach (SVR_JMMUser juser in aniDBUsers)
                            if (juser.IsAniDBUser == 1)
                                ep.SaveWatchedStatus(false, juser.JMMUserID, watchedDate, true);

                    ser = ep.GetAnimeSeries();
                    // a problem
                    if (ser == null) continue;
                    if (!toUpdateSeries.ContainsKey(ser.AnimeSeriesID))
                        toUpdateSeries.Add(ser.AnimeSeriesID, ser);

                    if (syncTrakt && ServerSettings.Instance.TraktTv.Enabled &&
                        !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    {
                        var cmdSyncTrakt = commandFactory.Create<CommandRequest_TraktHistoryEpisode>(
                            c =>
                            {
                                c.AnimeEpisodeID = ep.AnimeEpisodeID;
                                c.Action = (int)TraktSyncAction.Remove;
                            }
                        );
                        cmdSyncTrakt.Save();
                    }
                }
            }
        }


        // update stats for groups and series
        if (toUpdateSeries.Count > 0 && updateStats)
            foreach (SVR_AnimeSeries s in toUpdateSeries.Values)
                // update all the groups above this series in the heirarchy
                s.UpdateStats(true, true, true);
    }

    public override string ToString()
    {
        return $"{FileName} --- {Hash}";
    }


    public CL_VideoLocal ToClient(int userID)
    {
        CL_VideoLocal cl = new CL_VideoLocal
        {
            CRC32 = CRC32,
            DateTimeUpdated = DateTimeUpdated,
            FileName = FileName,
            FileSize = FileSize,
            Hash = Hash,
            HashSource = HashSource,
            IsIgnored = IsIgnored,
            IsVariation = IsVariation,
            Duration = (long) (Media?.GeneralStream.Duration ?? 0),
            MD5 = MD5,
            SHA1 = SHA1,
            VideoLocalID = VideoLocalID,
            Places = Places.Select(a => a.ToClient()).ToList()
        };
        SVR_VideoLocal_User userRecord = GetUserRecord(userID);
        if (userRecord?.WatchedDate == null)
        {
            cl.IsWatched = 0;
            cl.WatchedDate = null;
        }
        else
        {
            cl.IsWatched = 1;
            cl.WatchedDate = userRecord.WatchedDate;
        }
        cl.ResumePosition = userRecord?.ResumePosition ?? 0;

        try
        {

            if (Media?.GeneralStream != null) cl.Media = new Media(VideoLocalID, Media);
        }
        catch (Exception e)
        {
            logger.Error($"There was an error generating a Desktop client contract: {e}");
        }

        return cl;
    }

    // is the videolocal empty. This isn't complete, but without one or more of these the record is useless
    public bool IsEmpty()
    {
        if (!string.IsNullOrEmpty(Hash)) return false;
        if (!string.IsNullOrEmpty(MD5)) return false;
        if (!string.IsNullOrEmpty(CRC32)) return false;
        if (!string.IsNullOrEmpty(SHA1)) return false;
        if (!string.IsNullOrEmpty(FileName)) return false;
        if (FileSize > 0) return false;
        return true;
    }

    public CL_VideoDetailed ToClientDetailed(int userID)
    {
        // get the cross ref episode
        var xrefs = EpisodeCrossRefs;
        if (xrefs.Count == 0) return null;

        var userRecord = GetUserRecord(userID);
        var aniFile = GetAniDBFile(); // to prevent multiple db calls
        var relGroup = ReleaseGroup; // to prevent multiple db calls
        var cl = new CL_VideoDetailed { Percentage = xrefs[0].Percentage, EpisodeOrder = xrefs[0].EpisodeOrder, CrossRefSource = xrefs[0].CrossRefSource, AnimeEpisodeID = xrefs[0].EpisodeID,
            VideoLocal_FileName = FileName,
            VideoLocal_Hash = Hash,
            VideoLocal_FileSize = FileSize,
            VideoLocalID = VideoLocalID,
            VideoLocal_IsIgnored = IsIgnored,
            VideoLocal_IsVariation = IsVariation,
            Places = Places.Select(a => a.ToClient()).ToList(),
            VideoLocal_MD5 = MD5,
            VideoLocal_SHA1 = SHA1,
            VideoLocal_CRC32 = CRC32,
            VideoLocal_HashSource = HashSource,
            VideoLocal_IsWatched = userRecord?.WatchedDate == null ? 0 : 1,
            VideoLocal_WatchedDate = userRecord?.WatchedDate,
            VideoLocal_ResumePosition = userRecord?.ResumePosition ?? 0,
            VideoInfo_AudioBitrate = Media?.AudioStreams.FirstOrDefault()?.BitRate.ToString(),
            VideoInfo_AudioCodec = LegacyMediaUtils.TranslateCodec(Media?.AudioStreams.FirstOrDefault()),
            VideoInfo_Duration = Duration,
            VideoInfo_VideoBitrate = (Media?.VideoStream?.BitRate ?? 0).ToString(),
            VideoInfo_VideoBitDepth = (Media?.VideoStream?.BitDepth ?? 0).ToString(),
            VideoInfo_VideoCodec = LegacyMediaUtils.TranslateCodec(Media?.VideoStream),
            VideoInfo_VideoFrameRate = Media?.VideoStream?.FrameRate.ToString(),
            VideoInfo_VideoResolution = VideoResolution,
            AniDB_File_FileExtension = Path.GetExtension(aniFile?.FileName) ?? string.Empty,
            AniDB_File_LengthSeconds = (int?) Media?.General?.Duration ?? 0,
            AniDB_AnimeID = xrefs.FirstOrDefault()?.AnimeID,
            AniDB_CRC = CRC32,
            AniDB_MD5 = MD5,
            AniDB_SHA1 = SHA1,
            AniDB_Episode_Rating = 0,
            AniDB_Episode_Votes = 0,
            AniDB_File_AudioCodec = LegacyMediaUtils.TranslateCodec(Media?.AudioStreams.FirstOrDefault()) ?? string.Empty,
            AniDB_File_VideoCodec = LegacyMediaUtils.TranslateCodec(Media?.VideoStream) ?? string.Empty,
            AniDB_File_VideoResolution = VideoResolution,
            AniDB_Anime_GroupName = aniFile?.Anime_GroupName ?? string.Empty,
            AniDB_Anime_GroupNameShort = aniFile?.Anime_GroupNameShort ?? string.Empty,
            AniDB_File_Description = aniFile?.File_Description ?? string.Empty,
            AniDB_File_ReleaseDate = aniFile?.File_ReleaseDate ?? 0,
            AniDB_File_Source = aniFile?.File_Source ?? string.Empty,
            AniDB_FileID = aniFile?.FileID ?? 0,
            AniDB_GroupID = aniFile?.GroupID ?? 0,
            AniDB_File_FileVersion = aniFile?.FileVersion ?? 1,
            AniDB_File_IsCensored = aniFile?.IsCensored ?? false ? 1 : 0,
            AniDB_File_IsChaptered = aniFile?.IsChaptered ?? false ? 1 : 0,
            AniDB_File_IsDeprecated = aniFile?.IsDeprecated ?? false ? 1 : 0,
            AniDB_File_InternalVersion = aniFile?.InternalVersion ?? 3,
            LanguagesAudio = aniFile?.LanguagesRAW ?? string.Empty,
            LanguagesSubtitle = aniFile?.SubtitlesRAW ?? string.Empty,
            ReleaseGroup = relGroup,
            Media = Media == null ? null : new Media(VideoLocalID, Media),
        };
        return cl;
    }

    public CL_VideoLocal_ManualLink ToContractManualLink(int userID)
    {
        CL_VideoLocal_ManualLink cl = new CL_VideoLocal_ManualLink
        {
            CRC32 = CRC32,
            DateTimeUpdated = DateTimeUpdated,
            FileName = FileName,
            FileSize = FileSize,
            Hash = Hash,
            HashSource = HashSource,
            IsIgnored = IsIgnored,
            IsVariation = IsVariation,
            MD5 = MD5,
            SHA1 = SHA1,
            VideoLocalID = VideoLocalID,
            Places = Places.Select(a => a.ToClient()).ToList()
        };
        SVR_VideoLocal_User userRecord = GetUserRecord(userID);
        if (userRecord?.WatchedDate == null)
        {
            cl.IsWatched = 0;
            cl.WatchedDate = null;
        }
        else
        {
            cl.IsWatched = 1;
            cl.WatchedDate = userRecord.WatchedDate;
        }
        cl.ResumePosition = userRecord?.ResumePosition ?? 0;
        return cl;
    }

    public bool MergeInfoFrom(VideoLocal vl)
    {
        bool changed = false;
        if (string.IsNullOrEmpty(Hash) && !string.IsNullOrEmpty(vl.Hash))
        {
            Hash = vl.Hash;
            changed = true;
        }
        if (string.IsNullOrEmpty(CRC32) && !string.IsNullOrEmpty(vl.CRC32))
        {
            CRC32 = vl.CRC32;
            changed = true;
        }
        if (string.IsNullOrEmpty(MD5) && !string.IsNullOrEmpty(vl.MD5))
        {
            MD5 = vl.MD5;
            changed = true;
        }
        if (string.IsNullOrEmpty(SHA1) && !string.IsNullOrEmpty(vl.SHA1))
        {
            SHA1 = vl.SHA1;
            changed = true;
        }
        return changed;
    }
}

// This is a comparer used to sort the completeness of a videolocal, more complete first.
// Because this is only used for comparing completeness of hashes, it does NOT follow the strict equality rules
public class VideoLocalComparer : IComparer<VideoLocal>
{
    public int Compare(VideoLocal x, VideoLocal y)
    {
        if (x == null) return 1;
        if (y == null) return -1;
        if (string.IsNullOrEmpty(x.Hash)) return 1;
        if (string.IsNullOrEmpty(y.Hash)) return -1;
        if (string.IsNullOrEmpty(x.CRC32)) return 1;
        if (string.IsNullOrEmpty(y.CRC32)) return -1;
        if (string.IsNullOrEmpty(x.MD5)) return 1;
        if (string.IsNullOrEmpty(y.MD5)) return -1;
        if (string.IsNullOrEmpty(x.SHA1)) return 1;
        if (string.IsNullOrEmpty(y.SHA1)) return -1;
        return x.HashSource.CompareTo(y.HashSource);
    }
}
