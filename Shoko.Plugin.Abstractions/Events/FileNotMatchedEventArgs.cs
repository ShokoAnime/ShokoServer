using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions;

public class FileNotMatchedEventArgs : FileEventArgs
{
    /// <summary>
    /// Number of times we've tried to auto-match this file up until now.
    /// </summary>
    public int AutoMatchAttempts { get; }

    /// <summary>
    /// True if this file had existing cross-references before this match
    /// attempt.
    /// </summary>
    public bool HasCrossReferences { get; }

    /// <summary>
    /// True if we're currently UDP banned.
    /// </summary>
    public bool IsUDPBanned { get; }

    public FileNotMatchedEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo, int autoMatchAttempts, bool hasCrossReferences, bool isUdpBanned)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo)
    {
        AutoMatchAttempts = autoMatchAttempts;
        HasCrossReferences = hasCrossReferences;
        IsUDPBanned = isUdpBanned;
    }
}
