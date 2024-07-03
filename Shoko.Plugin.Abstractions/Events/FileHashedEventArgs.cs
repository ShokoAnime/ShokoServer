using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

public class FileHashedEventArgs : FileEventArgs
{
    public FileHashedEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<ISeries> animeInfo, IEnumerable<IGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }
}
