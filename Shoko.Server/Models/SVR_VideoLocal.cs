using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Release;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

using AniDB_ReleaseGroup = Shoko.Server.Models.AniDB.AniDB_ReleaseGroup;
using MediaContainer = Shoko.Models.MediaInfo.MediaContainer;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models;

public class SVR_VideoLocal : VideoLocal, IHashes, IVideo
{
    #region DB columns

    public new bool IsIgnored { get; set; }

    public new bool IsVariation { get; set; }

    public int MediaVersion { get; set; }

    /// <summary>
    /// Last time we did a successful AVDump.
    /// </summary>
    /// <value></value>
    public DateTime? LastAVDumped { get; set; }

    /// <summary>
    /// The Version of AVDump from Last time we did a successful AVDump.
    /// </summary>
    /// <value></value>
    public string? LastAVDumpVersion { get; set; }

    #endregion

    public int MyListID { get; set; }

    public bool IsManualLink => AniDBFile == null;

    /// <summary>
    /// Playback duration in milliseconds.
    /// </summary>
    /// <remarks>
    /// MediaInfo model has it in seconds, with milliseconds after the decimal point.
    /// </remarks>
    public long Duration => (long)(MediaInfo?.GeneralStream?.Duration * 1000 ?? 0);

    /// <summary>
    /// Playback duration as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan DurationTimeSpan
    {
        get
        {
            var duration = MediaInfo?.GeneralStream?.Duration ?? 0;
            var seconds = Math.Truncate(duration);
            var milliseconds = (duration - seconds) * 1000;
            return new TimeSpan(0, 0, 0, (int)seconds, (int)milliseconds);
        }
    }

    public string VideoResolution => MediaInfo?.VideoStream == null ? "0x0" : $"{MediaInfo.VideoStream.Width}x{MediaInfo.VideoStream.Height}";

    public string Info => string.IsNullOrEmpty(FileName) ? string.Empty : FileName;

    public const int MEDIA_VERSION = 5;

    public MediaContainer? MediaInfo { get; set; }

    public IReadOnlyList<SVR_VideoLocal_Place> Places => VideoLocalID is 0 ? [] : RepoFactory.VideoLocalPlace.GetByVideoLocal(VideoLocalID);

    public SVR_AniDB_File? AniDBFile => RepoFactory.AniDB_File.GetByHash(Hash);

    internal AniDB_ReleaseGroup? ReleaseGroup
    {
        get
        {
            if (AniDBFile is not { } anidbFile)
                return null;

            return RepoFactory.AniDB_ReleaseGroup.GetByGroupID(anidbFile.GroupID);
        }
    }

    public IReadOnlyList<SVR_AnimeEpisode> AnimeEpisodes
        => RepoFactory.AnimeEpisode.GetByHash(Hash);

    public IReadOnlyList<SVR_CrossRef_File_Episode> EpisodeCrossReferences =>
        string.IsNullOrEmpty(Hash) ? [] : RepoFactory.CrossRef_File_Episode.GetByEd2k(Hash);

    public SVR_VideoLocal_Place? FirstValidPlace
        => Places
            .Where(p => !string.IsNullOrEmpty(p?.FullServerPath))
            .MinBy(a => a.ImportFolderType);

    public SVR_VideoLocal_Place? FirstResolvedPlace
        => Places
            .Select(location => (location, importFolder: location.ImportFolder, fullPath: location.FullServerPath))
            .Where(tuple => tuple.importFolder is not null && !string.IsNullOrEmpty(tuple.fullPath))
            .OrderBy(a => a.importFolder!.ImportFolderType)
            .FirstOrDefault(p => File.Exists(p.fullPath)).location;

    public override string ToString()
    {
        return $"{FileName} --- {Hash}";
    }

    public string ToStringDetailed()
    {
        var sb = new StringBuilder("");
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

    // is the video local empty. This isn't complete, but without one or more of these the record is useless
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

    /// <summary>
    /// Checks if any of the hashes are empty
    /// </summary>
    /// <returns></returns>
    public bool HasAnyEmptyHashes()
    {
        if (string.IsNullOrEmpty(Hash)) return true;
        if (string.IsNullOrEmpty(MD5)) return true;
        if (string.IsNullOrEmpty(SHA1)) return true;
        if (string.IsNullOrEmpty(CRC32)) return true;
        return false;
    }

    #region IVideo Implementation

    string? IVideo.EarliestKnownName => RepoFactory.FileNameHash.GetByHash(Hash).MinBy(a => a.FileNameHashID)?.FileName;

    long IVideo.Size => FileSize;

    IReadOnlyList<IVideoFile> IVideo.Locations => Places;

    IReleaseInfo? IVideo.ReleaseInfo => null;

    IHashes IVideo.Hashes => this;

    IMediaInfo? IVideo.MediaInfo => MediaInfo;

    IReadOnlyList<IVideoCrossReference> IVideo.CrossReferences => EpisodeCrossReferences;

    IReadOnlyList<IShokoEpisode> IVideo.Episodes =>
        EpisodeCrossReferences
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToArray();

    IReadOnlyList<IShokoSeries> IVideo.Series =>
        EpisodeCrossReferences
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .OrderBy(a => a.PreferredTitle)
            .ToArray();

    IReadOnlyList<IShokoGroup> IVideo.Groups =>
        EpisodeCrossReferences
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .OrderBy(g => g.GroupName)
            .ToArray();

    int IMetadata<int>.ID => VideoLocalID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    Stream? IVideo.GetStream()
    {
        if (FirstResolvedPlace is not { } fileLocation)
            return null;

        var filePath = fileLocation.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
            return null;

        return File.OpenRead(filePath);
    }

    #endregion

    #region IHashes Implementation

    string? IHashes.CRC => CRC32;

    string? IHashes.MD5 => MD5;

    string IHashes.ED2K => Hash;

    string? IHashes.SHA1 => SHA1;

    string? IHashes.this[HashAlgorithmName algorithm]
        => algorithm switch
        {
            HashAlgorithmName.ED2K => Hash,
            HashAlgorithmName.MD5 => MD5,
            HashAlgorithmName.SHA1 => SHA1,
            HashAlgorithmName.CRC32 => CRC32,
            _ => null,
        };

    #endregion
}

// This is a comparer used to sort the completeness of a video local, more complete first.
// Because this is only used for comparing completeness of hashes, it does NOT follow the strict equality rules
public class VideoLocalComparer : IComparer<VideoLocal>
{
    public int Compare(VideoLocal? x, VideoLocal? y)
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
