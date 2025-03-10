using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models;

/// <summary>
/// A video file location tied to a video and managed folder at a relative path
/// within the managed folder.
/// </summary>
public class VideoLocal_Place : IVideoFile
{
    /// <inheritdoc/>
    public int ID { get; set; }

    /// <summary>
    /// The ID of the <see cref="VideoLocal"/> this video file belongs to.
    /// </summary>
    public int VideoID { get; set; }

    /// <inheritdoc/>
    public int ManagedFolderID { get; set; }

    private string _relativePath = string.Empty;

    /// <summary>
    /// Relative path to where the file is located within the
    /// <see cref="ShokoManagedFolder"/>. It will always use forward slashes (/)
    /// with no trailing or leading slashes.
    /// </summary>
    public string RelativePath
    {
        get => _relativePath;
        set => _relativePath = Utils.CleanPath(value, cleanStart: true);
    }

    #region Helpers

    /// <summary>
    /// Absolute OS dependent path to where the file lives. Will be null if the
    /// managed folder is unavailable or relative path is null/empty.
    /// </summary>
    public string? Path
    {
        get
        {
            var folderPath = ManagedFolder?.Path;
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(RelativePath))
                return null;

            return System.IO.Path.Join(folderPath, RelativePath);
        }
    }

    /// <summary>
    /// Indicates that the file exists.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Path))]
    [MemberNotNullWhen(true, nameof(RelativePath))]
    [MemberNotNullWhen(true, nameof(FileInfo))]
    public bool IsAvailable
        => File.Exists(Path);

    /// <summary>
    /// Helper to get the file name from the relative path.
    /// </summary>
    public string FileName
        => System.IO.Path.GetFileName(RelativePath);

    /// <summary>
    /// Helper to get the <see cref="VideoLocal"/> linked to the file location.
    /// </summary>
    public VideoLocal? VideoLocal
        => VideoID is 0 ? null : RepoFactory.VideoLocal.GetByID(VideoID);

    /// <summary>
    /// Helper to get the <see cref="ShokoManagedFolder"/> linked to the file location.
    /// </summary>
    public ShokoManagedFolder? ManagedFolder
        => ManagedFolderID is 0 ? null : RepoFactory.ShokoManagedFolder.GetByID(ManagedFolderID);

    /// <summary>
    /// Helper to get the <see cref="FileInfo"/> for the file location if it exists.
    /// </summary>
    public FileInfo? FileInfo
        => IsAvailable ? new FileInfo(Path) : null;

    #endregion

    #region IVideoFile Implementation

    IVideo IVideoFile.Video => VideoLocal
        ?? throw new NullReferenceException("Unable to get the associated IVideo for the IVideoFile with ID " + ID);

    string IVideoFile.Path => Path
        ?? throw new NullReferenceException("Unable to get the absolute path for the IVideoFile with ID " + ID);

    string IVideoFile.RelativePath => '/' + RelativePath;

    long IVideoFile.Size => VideoLocal?.FileSize
        ?? throw new NullReferenceException("Unable to get the size for the IVideoFile with ID " + ID);

    IManagedFolder IVideoFile.ManagedFolder => ManagedFolder
        ?? throw new NullReferenceException("Unable to get the associated IManagedFolder for the IVideoFile with ID " + ID);

    Stream? IVideoFile.GetStream()
    {
        var filePath = Path;
        if (string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
            return null;

        return File.OpenRead(filePath);
    }

    #endregion
}
