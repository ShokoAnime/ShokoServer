using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file is not matched, be it when it's the first check and
/// there isn't a match yet, or for existing matches if it didn't change from
/// the last check, or if we're UDP banned. Look at
/// <see cref="AutoMatchAttempts"/>, <see cref="HasCrossReferences"/> and/or
/// <see cref="IsUDPBanned"/> for more info.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNotMatchedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="importFolder">Import folder.</param>
    /// <param name="fileInfo">File info.</param>
    /// <param name="videoInfo">Video info.</param>
    /// <param name="episodeInfo">Episode info.</param>
    /// <param name="animeInfo">Series info.</param>
    /// <param name="groupInfo">Group info.</param>
    /// <param name="autoMatchAttempts">Number of times we've tried to auto-match this file up until now.</param>
    /// <param name="hasCrossReferences">True if this file had existing cross-references before this match attempt.</param>
    /// <param name="isUdpBanned">True if we're currently UDP banned.</param>
    public FileNotMatchedEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo, int autoMatchAttempts, bool hasCrossReferences, bool isUdpBanned)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo)
    {
        AutoMatchAttempts = autoMatchAttempts;
        HasCrossReferences = hasCrossReferences;
        IsUDPBanned = isUdpBanned;
    }
}
