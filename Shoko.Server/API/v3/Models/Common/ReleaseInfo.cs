
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.v3.Models.Common;

public class ReleaseInfo
{
    public ReleaseInfo(IReleaseInfo releaseInfo)
    {
        ID = int.Parse(releaseInfo.ReleaseURI![23..]);
        Source = ParseFileSource(releaseInfo.Source);
        ReleaseGroup = releaseInfo.Group is { } group ? new(group) : new();
        ReleaseDate = releaseInfo.ReleasedAt;
        Version = releaseInfo.Revision;
        IsDeprecated = releaseInfo.IsCorrupted;
        IsCensored = releaseInfo.IsCensored ?? false;
        Chaptered = releaseInfo.IsChaptered ?? false;
        OriginalFileName = releaseInfo.OriginalFilename;
        FileSize = releaseInfo.FileSize ?? 0L;
        Description = releaseInfo.Comment;
        Updated = releaseInfo.LastUpdatedAt.ToUniversalTime();
        AudioLanguages = releaseInfo.MediaInfo?.AudioLanguages.Select(a => a.GetString()).ToList() ?? [];
        SubLanguages = releaseInfo.MediaInfo?.SubtitleLanguages.Select(a => a.GetString()).ToList() ?? [];
    }

    /// <summary>
    /// The AniDB File ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Blu-ray, DVD, LD, TV, etc
    /// </summary>
    public FileSource Source { get; set; }

    /// <summary>
    /// The Release Group. This is usually set, but sometimes is set as "raw/unknown"
    /// </summary>
    public ReleaseGroup ReleaseGroup { get; set; }

    /// <summary>
    /// The file's release date. This is probably not filled in
    /// </summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>
    /// The file's version, Usually 1, sometimes more when there are edits released later
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Is the file marked as deprecated. Generally, yes if there's a V2, and this isn't it
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Mostly applicable to hentai, but on occasion a TV release is censored enough to earn this.
    /// </summary>
    public bool? IsCensored { get; set; }

    /// <summary>
    /// The original FileName. Useful for when you obtained from a shady source or when you renamed it without thinking. 
    /// </summary>
    public string OriginalFileName { get; set; }

    /// <summary>
    /// The reported FileSize. If you got this far and it doesn't match, something very odd has occurred
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Any comments that were added to the file, such as something wrong with it.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The audio languages
    /// </summary>
    public List<string> AudioLanguages { get; set; }

    /// <summary>
    /// Sub languages
    /// </summary>
    public List<string> SubLanguages { get; set; }

    /// <summary>
    /// Does the file have chapters. This may be wrong, since it was only added in AVDump2 (a more recent version at that)
    /// </summary>
    public bool Chaptered { get; set; }

    /// <summary>
    /// When we last got data on this file
    /// </summary>
    public DateTime Updated { get; set; }

    public static FileSource ParseFileSource(ReleaseSource source)
        => source switch
        {
            ReleaseSource.TV => FileSource.TV,
            ReleaseSource.DVD => FileSource.DVD,
            ReleaseSource.BluRay => FileSource.BluRay,
            ReleaseSource.Web => FileSource.Web,
            ReleaseSource.VHS => FileSource.VHS,
            ReleaseSource.VCD => FileSource.VCD,
            ReleaseSource.LaserDisc => FileSource.LaserDisc,
            ReleaseSource.Camera => FileSource.Camera,
            ReleaseSource.Other => FileSource.Other,
            _ => FileSource.Unknown,
        };
}
