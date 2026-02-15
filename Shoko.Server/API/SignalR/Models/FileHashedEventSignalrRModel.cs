using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Hashing;

namespace Shoko.Server.API.SignalR.Models;

public class FileHashedEventSignalRModel : FileEventSignalRModel
{
    public FileHashedEventSignalRModel(FileHashedEventArgs eventArgs) : base(eventArgs)
    {
        UsedExistingHashes = eventArgs.UsedExistingHashes;
        IsNewVideo = eventArgs.IsNewVideo;
        IsNewFile = eventArgs.IsNewFile;
        Hashes = eventArgs.Hashes.Select(h => new HashDigest() { Type = h.Type, Value = h.Value, Metadata = h.Metadata }).ToList();
    }

    /// <summary>
    /// Indicates that the hashes may have been reused from an existing video.
    /// </summary>
    public bool UsedExistingHashes { get; }

    /// <summary>
    /// Indicates that the video was just added to the database as a result of
    /// this operation.
    /// </summary>
    public bool IsNewVideo { get; }

    /// <summary>
    /// Indicates that the file was just added to the database as a result of
    /// this operation.
    /// </summary>
    public bool IsNewFile { get; }

    /// <summary>
    /// The hashes that were the result of the operation. May or may not have
    /// been reused depending on the provider(s) enabled and if it was requested
    /// to re-use existing hashes.
    /// </summary>
    public IReadOnlyList<HashDigest> Hashes { get; }
}
