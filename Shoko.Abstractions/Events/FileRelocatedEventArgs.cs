using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when a file is moved or renamed.
/// </summary>
public class FileRelocatedEventArgs : FileEventArgs
{
    /// <summary>
    /// The previous relative path of the file from the
    /// <see cref="PreviousManagedFolder"/>'s base location.
    /// </summary>
    public string PreviousRelativePath { get; set; }

    /// <summary>
    /// The previous managed folder that the file was in.
    /// </summary>
    public IManagedFolder PreviousManagedFolder { get; set; }

    /// <summary>
    /// Whether or not the file was moved.
    /// </summary>
    public bool Moved => !string.Equals(Path.GetDirectoryName(RelativePath), Path.GetDirectoryName(PreviousRelativePath), System.StringComparison.InvariantCulture) || PreviousManagedFolder != ManagedFolder;

    /// <summary>
    /// Whether or not the file was renamed.
    /// </summary>
    public bool Renamed => Path.GetFileName(RelativePath) != Path.GetFileName(PreviousRelativePath);

    /// <summary>
    /// The absolute path leading to the previous location of the file. Uses an OS dependent directory separator.
    /// </summary>
    public string PreviousPath => Path.Join(PreviousManagedFolder.Path, PreviousRelativePath);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRelocatedEventArgs"/> class.
    /// </summary>
    /// <param name="relativePath">Relative path to the file.</param>
    /// <param name="managedFolder">The managed folder that the file is in.</param>
    /// <param name="previousRelativePath">Previous relative path to the file from the <paramref name="previousManagedFolder"/>'s base location.</param>
    /// <param name="previousManagedFolder">Previous managed folder that the file was in.</param>
    /// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
    /// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
    /// <param name="episodeInfo">The collection of <see cref="IShokoEpisode"/> information for the file.</param>
    /// <param name="animeInfo">The collection of <see cref="IShokoSeries"/> information for the file.</param>
    /// /// <param name="groupInfo">The collection of <see cref="IShokoGroup"/> information for the file.</param>
    public FileRelocatedEventArgs(string relativePath, IManagedFolder managedFolder, string previousRelativePath, IManagedFolder previousManagedFolder, IVideoFile fileInfo, IVideo videoInfo, IEnumerable<IShokoEpisode> episodeInfo, IEnumerable<IShokoSeries> animeInfo, IEnumerable<IShokoGroup> groupInfo)
        : base(relativePath, managedFolder, fileInfo, videoInfo, episodeInfo, animeInfo, groupInfo)
    {
        previousRelativePath = previousRelativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (previousRelativePath[0] != Path.DirectorySeparatorChar)
            previousRelativePath = Path.DirectorySeparatorChar + previousRelativePath;
        PreviousRelativePath = previousRelativePath;
        PreviousManagedFolder = previousManagedFolder;
    }
}
