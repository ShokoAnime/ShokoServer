using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions;

public class FileHashedEventArgs : FileEventArgs
{
    public FileHashedEventArgs(string relativePath, IImportFolder importFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IEpisode> episodeInfo, IEnumerable<IAnime> animeInfo, IEnumerable<IGroup> groupInfo)
        : base(relativePath, importFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo) { }
}
