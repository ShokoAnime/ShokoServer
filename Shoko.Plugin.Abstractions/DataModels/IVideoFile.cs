using System.IO;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Video file location.
/// </summary>
public interface IVideoFile
{
    /// <summary>
    /// The video file location (VideoLocal_Place) id.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// The video (VideoLocal) id.
    /// </summary>
    int VideoID { get; }

    /// <summary>
    /// The import folder id.
    /// </summary>
    int ImportFolderID { get; }

    /// <summary>
    /// True if the file currently exists on disk and is usable by Shoko.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// The file name.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The absolute path leading to the location of the file. Uses an OS dependent directory separator.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/> to the location of the file. Will always use forward slash as a directory
    /// separator, and will always start with a leading slash.
    /// <br/>
    /// E.g.
    /// "C:\absolute\relative\path.ext" becomes "/relative/path.ext" if "C:\absolute" is the import folder.
    /// or
    /// "/absolute/relative/path.ext" becomes "/relative/path.ext" if "/absolute" is the import folder.
    /// </summary>
    string RelativePath { get; }

    /// <summary>
    /// The file size, in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Get the video tied to the video file location.
    /// </summary>
    /// <value></value>
    IVideo Video { get; }

    /// <summary>
    /// The import folder tied to the video file location.
    /// </summary>
    IImportFolder ImportFolder { get; }

    /// <summary>
    /// Get the stream for the video file, if the file is still available.
    /// </summary>
    Stream? GetStream();
}
