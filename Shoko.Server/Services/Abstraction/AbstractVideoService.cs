using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services.Abstraction;

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
    public event EventHandler<FileHashedEventArgs>? VideoFileHashed;

    /// <inheritdoc/>
    public event EventHandler<FileRelocatedEventArgs>? VideoFileRelocated;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderAdded;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderUpdated;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderRemoved;

    private readonly VideoLocal_PlaceRepository _placeRepository;

    private readonly VideoLocalRepository _videoLocalRepository;

    private readonly VideoLocal_HashDigestRepository _videoLocalHashRepository;

    private readonly ShokoManagedFolderRepository _managedFolderRepository;

    private readonly IVideoHashingService _videoHashingService;

    public AbstractVideoService(
        VideoLocal_PlaceRepository placeRepository,
        VideoLocalRepository videoLocalRepository,
        VideoLocal_HashDigestRepository videoLocalHashRepository,
        ShokoManagedFolderRepository managedFolderRepository,
        IVideoHashingService videoHashingService
    )
    {
        _placeRepository = placeRepository;
        _videoLocalRepository = videoLocalRepository;
        _videoLocalHashRepository = videoLocalHashRepository;
        _managedFolderRepository = managedFolderRepository;
        _videoHashingService = videoHashingService;

        ShokoEventHandler.Instance.FileDeleted += OnFileDeleted;
        _videoHashingService.FileHashed += OnFileHashed;
        ShokoEventHandler.Instance.FileRelocated += OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded += OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated += OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved += OnManagedFolderRemoved;
    }

    ~AbstractVideoService()
    {
        ShokoEventHandler.Instance.FileDeleted -= OnFileDeleted;
        _videoHashingService.FileHashed -= OnFileHashed;
        ShokoEventHandler.Instance.FileRelocated -= OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded -= OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated -= OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved -= OnManagedFolderRemoved;
    }

    private void OnFileDetected(object? sender, FileDetectedEventArgs eventArgs)
    {
        VideoFileDetected?.Invoke(this, eventArgs);
    }

    private void OnFileDeleted(object? sender, FileEventArgs eventArgs)
    {
        VideoFileDeleted?.Invoke(this, eventArgs);
    }

    private void OnFileHashed(object? sender, FileHashedEventArgs eventArgs)
    {
        VideoFileHashed?.Invoke(this, eventArgs);
    }

    private void OnFileRelocated(object? sender, FileRelocatedEventArgs eventArgs)
    {
        VideoFileRelocated?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderAdded(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderAdded?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderUpdated(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderUpdated?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderRemoved(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderRemoved?.Invoke(this, eventArgs);
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
            .FirstOrDefault(a => string.Equals(a.Path, absolutePath, _platformComparison));

    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByRelativePath(string relativePath, int? managedFolderID = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (managedFolderID is null)
            return _placeRepository.GetByRelativePath(relativePath);

        if (managedFolderID is >= 0)
            return null;

        return _placeRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolderID.Value);
    }

    /// <inheritdoc/>
    public IEnumerable<IVideo> GetAllVideos()
        => _videoLocalRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IVideo? GetVideoByID(int videoID)
        => videoID <= 0 ? null : _videoLocalRepository.GetByID(videoID);

    /// <inheritdoc/>
    public IVideo? GetVideoByHash(string hash, string algorithm = "ED2K")
        => GetAllVideoByHash(hash, algorithm) is { Count: 1 } videos ? videos[0] : null;

    /// <inheritdoc/>
    public IVideo? GetVideoByHashAndSize(string hash, long fileSize, string algorithm = "ED2K")
        => GetAllVideoByHash(hash, algorithm).Where(a => a.Size == fileSize).ToList() is { Count: 1 } videos ? videos[0] : null;

    /// <inheritdoc/>
    public IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm = "ED2K")
        => _videoLocalHashRepository.GetByHashTypeAndValue(algorithm, hash)
            .Select(a => _videoLocalRepository.GetByID(a.VideoLocalID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm, string? metadata)
        => _videoLocalHashRepository.GetByHashTypeAndValue(algorithm, hash)
            .Where(a => string.Equals(a.Metadata, metadata, StringComparison.Ordinal))
            .Select(a => _videoLocalRepository.GetByID(a.VideoLocalID))
            .WhereNotNull()
            .ToList();

    /// <inheritdoc/>
    public IEnumerable<IManagedFolder> GetAllManagedFolders()
        => _managedFolderRepository.GetAll();

    /// <inheritdoc/>
    public IManagedFolder? GetManagedFolderByID(int folderID)
        => folderID <= 0 ? null : _managedFolderRepository.GetByID(folderID);
}

