using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions;

public class FileHashedEventArgs : FileEventArgs
{
    public IHashes Hashes { get; set; }
    public IMediaContainer MediaInfo { get; set; }
}
