using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

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

    /// <inheritdoc/>
    public bool IsKept { get; set; }

    /// <summary>
    /// The primary location of the file group this location is part of: its own ID
    /// when it is the group's primary, or alone, which it also reads as until
    /// one is set, so it is never <c>0</c> once the location has an ID.
    /// Locations that are the same file, hard links of each other and symbolic
    /// links with the file they point to, share one, kept up to date by scans
    /// (<c>VideoService.RepairVideoFileGroups</c>).
    /// </summary>
    public int PrimaryLocationID
    {
        get => _primaryLocationID is 0 ? ID : _primaryLocationID;
        set => _primaryLocationID = value;
    }

    private int _primaryLocationID;

    /// <summary>
    /// Whether <see cref="PrimaryLocationID"/> was set, rather than reading as
    /// the location's own ID because it was not.
    /// </summary>
    internal bool HasPrimaryLocationID
        => _primaryLocationID is not 0;

    /// <inheritdoc/>
    int IVideoFile.PrimaryFileID => PrimaryLocationID;

    /// <inheritdoc/>
    public bool IsSymbolicLink { get; set; }

    /// <inheritdoc/>
    public string? SymbolicLinkTarget { get; set; }

    /// <inheritdoc/>
    public SymbolicLinkStat? LinkStat
    {
        get
        {
            try
            {
                return ISystemService.StaticServices.GetRequiredService<IFileSystem>().GetStat(Path, followSymbolicLinks: false) as SymbolicLinkStat;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <inheritdoc/>
    public FileStat? Stat
    {
        get
        {
            try
            {
                return ISystemService.StaticServices.GetRequiredService<IFileSystem>().GetFileStat(Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    private string _relativePath = string.Empty;

    private string? _platformRelativePath;

    /// <summary>
    /// Relative path to where the file is located within the
    /// <see cref="ShokoManagedFolder"/>. It will always use forward slashes (/)
    /// with no trailing or leading slashes.
    /// </summary>
    public string RelativePath
    {
        get => _relativePath;
        set
        {
            _relativePath = PlatformUtility.NormalizePath(value, stripLeadingSlash: true);
            _platformRelativePath = null;
        }
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

            _platformRelativePath ??= PlatformUtility.NormalizePath(RelativePath, platformFormat: true);
            return System.IO.Path.Join(folderPath, _platformRelativePath);
        }
    }

    /// <summary>
    /// Indicates that the file exists.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Path))]
    [MemberNotNullWhen(true, nameof(RelativePath))]
    [MemberNotNullWhen(true, nameof(FileInfo))]
    public bool IsAvailable
        => ISystemService.StaticServices.GetRequiredService<IFileSystem>().FileExists(Path);

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
        => ISystemService.StaticServices.GetRequiredService<IFileSystem>().GetFileInfo(Path);

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

        var fileSystem = ISystemService.StaticServices.GetRequiredService<IFileSystem>();
        if (!fileSystem.FileExists(filePath))
            return null;

        return fileSystem.OpenRead(filePath);
    }

    #endregion
}
