using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

public class FileMatchedEventArgs : FileEventArgs
{
    public FileMatchedEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }
}

