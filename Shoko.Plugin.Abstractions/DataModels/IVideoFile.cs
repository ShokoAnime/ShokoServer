namespace Shoko.Plugin.Abstractions.DataModels;

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
    /// The file name.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The absolute path leading to the location of the file. Uses an OS dependent directory separator.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/> to the location of the file. Will always use forward slash as a directory separator.
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
    IVideo? Video { get; }

    /// <summary>
    /// The import folder tied to the video file location.
    /// </summary>
    IImportFolder? ImportFolder { get; }
}
