using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractVideoService : IVideoService
{
    private static readonly StringComparison _platformComparison = Utils.IsLinux
        ? StringComparison.InvariantCulture
        : StringComparison.InvariantCultureIgnoreCase;

    /// <inheritdoc/>
    public event EventHandler<FileDetectedEventArgs>? VideoFileDetected;

    /// <inheritdoc/>
    public event EventHandler<FileEventArgs>? VideoFileDeleted;

    /// <inheritdoc/>
    public event EventHandler<FileEventArgs>? VideoFileHashed;

    /// <inheritdoc/>
    public event EventHandler<FileRenamedEventArgs>? VideoFileRenamed;

    /// <inheritdoc/>
    public event EventHandler<FileMovedEventArgs>? VideoFileMoved;

    /// <inheritdoc/>
    public event EventHandler<FileMovedEventArgs>? VideoFileRelocated;

    private readonly VideoLocal_PlaceRepository _placeRepository;

    private readonly VideoLocalRepository _videoLocalRepository;

    public AbstractVideoService(
        VideoLocal_PlaceRepository placeRepository,
        VideoLocalRepository videoLocalRepository
    )
    {
        _placeRepository = placeRepository;
        _videoLocalRepository = videoLocalRepository;
        ShokoEventHandler.Instance.FileDetected += OnFileDetected;
        ShokoEventHandler.Instance.FileDeleted += OnFileDeleted;
        ShokoEventHandler.Instance.FileHashed += OnFileHashed;
        ShokoEventHandler.Instance.FileRenamed += OnFileRenamed;
        ShokoEventHandler.Instance.FileMoved += OnFileMoved;
    }

    ~AbstractVideoService()
    {
        ShokoEventHandler.Instance.FileDetected -= OnFileDetected;
        ShokoEventHandler.Instance.FileDeleted -= OnFileDeleted;
        ShokoEventHandler.Instance.FileHashed -= OnFileHashed;
        ShokoEventHandler.Instance.FileRenamed -= OnFileRenamed;
        ShokoEventHandler.Instance.FileMoved -= OnFileMoved;
    }

    private void OnFileDetected(object? sender, FileDetectedEventArgs eventArgs)
    {
        VideoFileDetected?.Invoke(this, eventArgs);
    }

    private void OnFileDeleted(object? sender, FileEventArgs eventArgs)
    {
        VideoFileDeleted?.Invoke(this, eventArgs);
    }

    private void OnFileHashed(object? sender, FileEventArgs eventArgs)
    {
        VideoFileHashed?.Invoke(this, eventArgs);
    }

    private void OnFileRenamed(object? sender, FileRenamedEventArgs eventArgs)
    {
        var moveEventArgs = new FileMovedEventArgs(
            eventArgs.RelativePath,
            eventArgs.ImportFolder,
            eventArgs.PreviousRelativePath,
            eventArgs.ImportFolder,
            eventArgs.File,
            eventArgs.Video,
            eventArgs.Episodes,
            eventArgs.Series,
            eventArgs.Groups
        );
        VideoFileRelocated?.Invoke(this, moveEventArgs);
        VideoFileRenamed?.Invoke(this, eventArgs);
    }

    private void OnFileMoved(object? sender, FileMovedEventArgs eventArgs)
    {
        VideoFileRelocated?.Invoke(this, eventArgs);
        VideoFileMoved?.Invoke(this, eventArgs);
    }

    /// <inheritdoc/>
    public IEnumerable<IVideoFile> GetAllVideoFiles()
        => _placeRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByID(int fileID)
        => fileID <= 0 ? null : _placeRepository.GetByID(fileID);

    // This will be slow for now, but at least it gets the job done.
    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByAbsolutePath(string absolutePath)
        => string.IsNullOrWhiteSpace(absolutePath) ? null : _placeRepository.GetAll()
            .FirstOrDefault(a => string.Equals(a.FullServerPath, absolutePath, _platformComparison));

    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByRelativePath(string relativePath, int? importFolderID = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (importFolderID is null)
            return _placeRepository.GetByFilePath(relativePath);

        if (importFolderID is >= 0)
            return null;

        return _placeRepository.GetByFilePathAndImportFolderID(relativePath, importFolderID.Value);
    }

    /// <inheritdoc/>
    public IEnumerable<IVideo> GetAllVideos()
        => _videoLocalRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IVideo? GetVideoByID(int videoID)
        => videoID <= 0 ? null : _videoLocalRepository.GetByID(videoID);

    /// <inheritdoc/>
    public IVideo? GetVideoByHash(string hash, HashAlgorithmName algorithm = HashAlgorithmName.ED2K)
        => algorithm switch
        {
            HashAlgorithmName.MD5 => _videoLocalRepository.GetByMd5(hash),
            HashAlgorithmName.SHA1 => _videoLocalRepository.GetBySha1(hash),
            HashAlgorithmName.CRC32 => _videoLocalRepository.GetByCrc32(hash),
            _ => _videoLocalRepository.GetByEd2k(hash),
        };

    /// <inheritdoc/>
    public IVideo? GetVideoByHashAndSize(string hash, long fileSize, HashAlgorithmName algorithm = HashAlgorithmName.ED2K)
        => algorithm switch
        {
            HashAlgorithmName.MD5 => _videoLocalRepository.GetByMd5AndSize(hash, fileSize),
            HashAlgorithmName.SHA1 => _videoLocalRepository.GetBySha1AndSize(hash, fileSize),
            HashAlgorithmName.CRC32 => _videoLocalRepository.GetByCrc32AndSize(hash, fileSize),
            _ => _videoLocalRepository.GetByEd2kAndSize(hash, fileSize),
        };
}

