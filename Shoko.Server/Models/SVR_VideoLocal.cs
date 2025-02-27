using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories;

using MediaContainer = Shoko.Models.MediaInfo.MediaContainer;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models;

public class SVR_VideoLocal : IHashes, IVideo
{
    #region DB columns

    public int VideoLocalID { get; set; }

    public string Hash { get; set; } = string.Empty;

    public int HashSource { get; set; }

    public long FileSize { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public DateTime? DateTimeImported { get; set; }

    [Obsolete("Use VideoLocal_Place.FilePath instead")]
    public string FileName { get; set; } = string.Empty;

    public bool IsIgnored { get; set; }

    public bool IsVariation { get; set; }

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

    public string VideoResolution =>
        MediaInfo?.VideoStream == null ? "0x0" : $"{MediaInfo.VideoStream.Width}x{MediaInfo.VideoStream.Height}";

    public string Info =>
        string.IsNullOrEmpty(FileName) ? string.Empty : FileName;

    public const int MEDIA_VERSION = 5;

    public MediaContainer? MediaInfo { get; set; }

    public IReadOnlyList<SVR_VideoLocal_Place> Places =>
        VideoLocalID is 0 ? [] : RepoFactory.VideoLocalPlace.GetByVideoLocal(VideoLocalID);

    public StoredReleaseInfo? ReleaseInfo =>
        string.IsNullOrEmpty(Hash) ? null : RepoFactory.StoredReleaseInfo.GetByEd2kAndFileSize(Hash, FileSize);

    internal IReleaseGroup? ReleaseGroup =>
        ((IReleaseInfo?)ReleaseInfo)?.Group;

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
        if (!string.IsNullOrEmpty(FileName)) return false;
        if (FileSize > 0) return false;
        return true;
    }

    #region IVideo Implementation

    string? IVideo.EarliestKnownName => RepoFactory.FileNameHash.GetByHash(Hash).MinBy(a => a.FileNameHashID)?.FileName;

    long IVideo.Size => FileSize;

    IReadOnlyList<IVideoFile> IVideo.Locations => Places;

    IReleaseInfo? IVideo.ReleaseInfo => ReleaseInfo;

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


    public string ED2K => Hash;

    public string CRC32 =>
        Hashes?.FirstOrDefault(h => h.Type is "CRC32")?.Value ?? string.Empty;

    public string MD5 =>
        Hashes?.FirstOrDefault(h => h.Type is "MD5")?.Value ?? string.Empty;

    public string SHA1 =>
        Hashes?.FirstOrDefault(h => h.Type is "SHA1")?.Value ?? string.Empty;

    string IHashes.SHA256 =>
        Hashes?.FirstOrDefault(h => h.Type is "SHA256")?.Value ?? string.Empty;

    string IHashes.SHA512 =>
        Hashes?.FirstOrDefault(h => h.Type is "SHA512")?.Value ?? string.Empty;

    public IReadOnlyList<VideoLocal_HashDigest> Hashes
        => RepoFactory.VideoLocalHashDigest.GetByVideoLocalID(VideoLocalID);

    IReadOnlyList<IHashDigest> IHashes.Hashes => Hashes;

    #endregion
}

// This is a comparer used to sort the completeness of a video local, more complete first.
// Because this is only used for comparing completeness of hashes, it does NOT follow the strict equality rules
public class VideoLocalComparer : IComparer<SVR_VideoLocal>
{
    public int Compare(SVR_VideoLocal? x, SVR_VideoLocal? y)
    {
        if (x == null) return 1;
        if (y == null) return -1;
        if (string.IsNullOrEmpty(x.Hash)) return 1;
        if (string.IsNullOrEmpty(y.Hash)) return -1;
        return x.HashSource.CompareTo(y.HashSource);
    }
}
