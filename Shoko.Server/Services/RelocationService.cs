using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Quartz;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Relocation;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.MediaInfo.Subtitles;
using Shoko.Server.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class RelocationService(
    ILogger<RelocationService> logger,
    IServiceProvider serviceProvider,
    IPluginManager pluginManager,
    ISettingsProvider settingsProvider,
    ISchedulerFactory schedulerFactory,
    IConfigurationService configurationService,
    FileWatcherService fileWatcherService,
    VideoLocal_PlaceRepository videoLocalPlace,
    StoredRelocationPipeRepository storedRelocationPipeRepository,
    FileNameHashRepository fileNameHash
) : IRelocationService
{
    private Dictionary<Guid, RelocationProviderInfo> _relocationProviderInfos = [];

    private VideoLocal_PlaceService? __videoLocalPlaceService;

    private VideoLocal_PlaceService _videoLocalPlaceService => __videoLocalPlaceService ??= serviceProvider.GetRequiredService<VideoLocal_PlaceService>();

    private readonly Dictionary<Type, MethodInfo?> _genericGetNewPaths = [];

    private readonly object _lock = new();

    private bool _loaded = false;

    #region Events

    /// <inheritdoc/>
    public event EventHandler? ProvidersUpdated;

    /// <inheritdoc/>
    public event EventHandler<RelocationPipeEventArgs>? PipeStored;

    public event EventHandler<RelocationPipeEventArgs>? PipeUpdated;

    public event EventHandler<RelocationPipeEventArgs>? PipeDeleted;

    public event EventHandler<FileRelocatedEventArgs>? FileRelocated;

    #endregion

    #region Settings

    public bool RelocateOnImport
    {
        get => settingsProvider.GetSettings().Plugins.Renamer.RelocateOnImport;
    }

    public bool RenameOnImport
    {
        get => settingsProvider.GetSettings().Plugins.Renamer.RenameOnImport;
        set
        {
            if (settingsProvider.GetSettings().Plugins.Renamer.RenameOnImport != value)
            {
                settingsProvider.GetSettings().Plugins.Renamer.RenameOnImport = value;
                settingsProvider.SaveSettings();

                Task.Run(() => ProvidersUpdated?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    public bool MoveOnImport
    {
        get => settingsProvider.GetSettings().Plugins.Renamer.MoveOnImport;
        set
        {
            if (settingsProvider.GetSettings().Plugins.Renamer.MoveOnImport != value)
            {
                settingsProvider.GetSettings().Plugins.Renamer.MoveOnImport = value;
                settingsProvider.SaveSettings();

                Task.Run(() => ProvidersUpdated?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    public bool AllowRelocationInsideDestinationOnImport
    {
        get => settingsProvider.GetSettings().Plugins.Renamer.AllowRelocationInsideDestinationOnImport;
        set
        {
            if (settingsProvider.GetSettings().Plugins.Renamer.AllowRelocationInsideDestinationOnImport != value)
            {
                settingsProvider.GetSettings().Plugins.Renamer.AllowRelocationInsideDestinationOnImport = value;
                settingsProvider.SaveSettings();

                Task.Run(() => ProvidersUpdated?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    #endregion

    #region Add Parts

    public void AddParts(IEnumerable<IRelocationProvider> providers)
    {
        if (_loaded) return;
        _loaded = true;

        logger.LogInformation("Initializing service.");

        lock (_lock)
        {
            _relocationProviderInfos = providers
                .Select(provider =>
                {
                    var providerType = provider.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(providerType.Assembly)!;
                    var id = GetID(providerType, pluginInfo);
                    var description = provider.Description?.CleanDescription() ?? string.Empty;
                    var configurationType = providerType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRelocationProvider<>))
                        ?.GetGenericArguments()[0];
                    var configurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType);
                    return new RelocationProviderInfo()
                    {
                        ID = id,
                        Version = provider.Version,
                        Name = provider.Name,
                        Description = description,
                        Provider = provider,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                    };
                })
                .OrderByDescending(info => typeof(CorePlugin) == info.PluginInfo.PluginType)
                .ThenBy(info => info.PluginInfo.Name)
                .ThenBy(info => info.Name)
                .ThenBy(info => info.ID)
                .Select((info, priority) => new RelocationProviderInfo()
                {
                    ID = info.ID,
                    Version = info.Version,
                    Name = info.Name,
                    Description = info.Description,
                    Provider = info.Provider,
                    ConfigurationInfo = info.ConfigurationInfo,
                    PluginInfo = info.PluginInfo,
                })
                .ToDictionary(info => info.ID);
        }

        logger.LogInformation("Loaded {ProviderCount} providers.", _relocationProviderInfos.Count);
    }

    #endregion Add Parts

    #region Providers

    public IEnumerable<RelocationProviderInfo> GetAvailableProviders()
        => _relocationProviderInfos.Values
            .OrderByDescending(info => typeof(CorePlugin) == info.PluginInfo.PluginType)
            .ThenBy(info => info.PluginInfo.Name)
            .ThenBy(info => info.Name)
            .ThenBy(info => info.ID);

    public IReadOnlyList<RelocationProviderInfo> GetProviderInfo(IPlugin plugin)
        => _relocationProviderInfos.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Name)
            .ThenBy(info => info.ID)
            .ToList();

    public RelocationProviderInfo GetProviderInfo(IRelocationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(provider.GetType()))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public RelocationProviderInfo GetProviderInfo<TProvider>() where TProvider : IRelocationProvider
    {
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public RelocationProviderInfo? GetProviderInfo(Guid providerID)
        => _relocationProviderInfos?.TryGetValue(providerID, out var providerInfo) ?? false
            ? providerInfo
            : null;

    #endregion

    #region Pipes

    public RelocationPipeInfo? GetDefaultPipe()
    {
        var settings = settingsProvider.GetSettings();
        return GetStoredPipe(settings.Plugins.Renamer.DefaultRenamer);
    }

    public IEnumerable<RelocationPipeInfo> GetStoredPipes(bool? available = null)
    {
        var pipes = storedRelocationPipeRepository.GetAll()
            .Select(pipe => new RelocationPipeInfo(this, configurationService, pipe));
        if (available is null) return pipes;
        return pipes.Where(p => p.ProviderInfo is null == available.Value);
    }

    public IReadOnlyList<RelocationPipeInfo> GetStoredPipes(Guid providerID)
        => storedRelocationPipeRepository
            .GetByProviderID(providerID)
            .Select(pipe => new RelocationPipeInfo(this, configurationService, pipe))
            .OrderBy(pipe => pipe.Name)
            .ThenBy(pipe => pipe.ID)
            .ToList();

    public IReadOnlyList<RelocationPipeInfo> GetStoredPipes(IRelocationProvider provider)
        => GetStoredPipes(GetID(provider.GetType()));

    public IReadOnlyList<RelocationPipeInfo> GetStoredPipes(IPlugin plugin)
        => GetProviderInfo(plugin)
            .SelectMany(info => GetStoredPipes(info.ID))
            .OrderBy(pipe => pipe.Name)
            .ThenBy(pipe => pipe.ID)
            .ToList();

    public RelocationPipeInfo? GetStoredPipe(Guid pipeID)
        => storedRelocationPipeRepository.GetByPipeID(pipeID) is { } pipe
            ? new RelocationPipeInfo(this, configurationService, pipe)
            : null;

    public RelocationPipeInfo? GetStoredPipe(string? name)
        => storedRelocationPipeRepository.GetByName(name) is { } pipe
            ? new RelocationPipeInfo(this, configurationService, pipe)
            : null;

    public RelocationPipeInfo StorePipe(IRelocationProvider provider, string name, IRelocationProviderConfiguration? configuration = null, bool setDefault = false)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrEmpty(name);
        name = name.Trim();

        // Ensure the name is unique by appending "(copy)" or "(copy #X)"
        if (storedRelocationPipeRepository.GetByName(name) is not null)
        {
            var index = 1;
            if (name.EndsWith(" (copy)", StringComparison.OrdinalIgnoreCase))
            {
                index = 2;
                name = name[..^7].Trim();
            }
            else if (Regex.Match(name, @" \(copy #(?<i>\d+)\)$") is { Success: true } match)
            {
                index = int.Parse(match.Groups["i"].Value) + 1;
                if (index is >= 100)
                    index = 1;
                else
                    name = name[..^match.Length].Trim();
            }

            var tempName = index is 1 ? $"{name} (copy)" : $"{name} (copy #{index})";
            while (storedRelocationPipeRepository.GetByName(tempName) is not null)
            {
                index++;
                if (index is >= 100)
                {
                    name += " (copy #99)";
                    index = 1;
                }
                tempName = index is 1 ? $"{name} (copy)" : $"{name} (copy #{index})";
            }
            name = tempName;
        }

        byte[]? configurationAsBytes = null;
        var providerInfo = GetProviderInfo(provider);
        if (providerInfo.ConfigurationInfo is not null)
        {
            if (configuration is null)
                configuration = (IRelocationProviderConfiguration)configurationService.New(providerInfo.ConfigurationInfo);
            else if (configurationService.Validate(providerInfo.ConfigurationInfo, configuration) is { Count: > 0 } errors)
                throw new ConfigurationValidationException("save", providerInfo.ConfigurationInfo, errors);
            if (providerInfo.ConfigurationInfo.Type != configuration.GetType())
                throw new InvalidOperationException("Configuration is not supported by this provider.");
            configurationAsBytes = Encoding.UTF8.GetBytes(
                configurationService.Serialize(configuration)
            );
        }
        else if (configuration is not null)
        {
            throw new InvalidOperationException("Configuration is not supported by this provider.");
        }

        var pipe = new StoredRelocationPipe()
        {
            ProviderID = providerInfo.ID,
            Name = name,
            Configuration = configurationAsBytes,
        };

        storedRelocationPipeRepository.Save(pipe);

        if (setDefault || storedRelocationPipeRepository.GetAll().Count is 1)
        {
            var settings = settingsProvider.GetSettings();
            if (settings.Plugins.Renamer.DefaultRenamer != pipe.Name)
            {
                settings.Plugins.Renamer.DefaultRenamer = pipe.Name;
                settingsProvider.SaveSettings();

                Task.Run(() => ProvidersUpdated?.Invoke(this, EventArgs.Empty));
            }
        }

        var pipeInfo = new RelocationPipeInfo(this, configurationService, pipe);

        Task.Run(() => PipeStored?.Invoke(this, new() { RelocationPipe = pipeInfo }));

        return pipeInfo;
    }

    public RelocationPipeInfo StorePipe<TConfig>(IRelocationProvider<TConfig> provider, string name, TConfig configuration, bool setDefault = false) where TConfig : IRelocationProviderConfiguration
        => StorePipe(provider, name, configuration, setDefault);

    public bool UpdatePipe(IStoredRelocationPipe pipe)
    {
        if (storedRelocationPipeRepository.GetByPipeID(pipe.ID) is not { } storedPipe)
            throw new InvalidOperationException("Stored pipe does not exist in the database.");

        var updated = false;
        if (pipe.Name != storedPipe.Name)
        {
            storedPipe.Name = pipe.Name;
            updated = true;
        }
        if (
            (storedPipe.Configuration is null && pipe.Configuration is not null) ||
            (storedPipe.Configuration is not null && pipe.Configuration is null) ||
            (storedPipe.Configuration is not null && pipe.Configuration is not null && !storedPipe.Configuration.SequenceEqual(pipe.Configuration))
        )
        {
            if (GetProviderInfo(storedPipe.ProviderID) is not { } providerInfo)
                throw new InvalidOperationException("Attempted to update the configuration for a pipe with an unregistered provider.");

            if (providerInfo.ConfigurationInfo is { } configurationInfo)
            {
                if (pipe.Configuration is null)
                    throw new InvalidOperationException("Cannot unset a configuration for a provider that does needs one.");

                var validationErrors = configurationService.Validate(configurationInfo, Encoding.UTF8.GetString(pipe.Configuration));
                if (validationErrors.Count > 0)
                    throw new ConfigurationValidationException("save", configurationInfo, validationErrors);
            }
            else
            {
                if (pipe.Configuration is not null)
                    throw new InvalidOperationException("Cannot set a configuration for a provider that does not support one.");
            }

            storedPipe.Configuration = pipe.Configuration;
            updated = true;
        }

        if (updated)
            storedRelocationPipeRepository.Save(storedPipe);

        var settings = settingsProvider.GetSettings();
        if (!storedPipe.IsDefault && (pipe.IsDefault || storedRelocationPipeRepository.GetAll().Count is 1))
        {
            if (settings.Plugins.Renamer.DefaultRenamer != storedPipe.Name)
            {
                settings.Plugins.Renamer.DefaultRenamer = storedPipe.Name;
                settingsProvider.SaveSettings();

                Task.Run(() => ProvidersUpdated?.Invoke(this, EventArgs.Empty));
            }
        }

        if (updated)
        {
            Task.Run(() => PipeUpdated?.Invoke(this, new() { RelocationPipe = new RelocationPipeInfo(this, configurationService, storedPipe) }));
        }

        return updated;
    }

    public void DeletePipe(IStoredRelocationPipe pipe)
    {
        if (storedRelocationPipeRepository.GetByPipeID(pipe.ID) is not { } storedPipe)
            throw new InvalidOperationException("Stored pipe does not exist in the database.");

        if (storedPipe.IsDefault)
            throw new InvalidOperationException("The default relocation pipe cannot be deleted.");

        storedRelocationPipeRepository.Delete(storedPipe);

        Task.Run(() => PipeDeleted?.Invoke(this, new() { RelocationPipe = new RelocationPipeInfo(this, configurationService, storedPipe) }));
    }

    #endregion

    #region Relocation Methods

    private enum DelayInUse
    {
        First = 750,
        Second = 3000,
        Third = 5000
    };

    public async Task ScheduleAutoRelocationForVideo(IVideo video, bool prioritize = false)
    {
        var fileName = video.Files is { Count: > 0 } files
            ? Path.GetFileName(files[0].RelativePath)
            : null;
        if (!RelocateOnImport)
        {
            logger.LogTrace("Auto-Relocation is disabled. Skipping relocation for video: {FileName} (Video={VideoID})", fileName, video.ID);
            return;
        }

        if (video.Files.DistinctBy(x => x.ManagedFolderID).All(x => x.ManagedFolder.DropFolderType is DropFolderType.Excluded))
        {
            logger.LogTrace("All files for video are not in a drop destination or source. Skipping relocation for video: {FileName} (Video={VideoID})", fileName, video.ID);
            return;
        }

        logger.LogTrace("Scheduling relocation for video: {FileName} (Video={VideoID})", fileName, video.ID);
        var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
        await scheduler.StartJob<RenameMoveFileJob>(b => b.VideoLocalID = video.ID, prioritize: prioritize).ConfigureAwait(false);
    }

    public async Task ScheduleAutoRelocationForVideoFile(IVideoFile file, bool prioritize = false)
    {
        var locationPath = file.Path;
        var folder = file is VideoLocal_Place place ? place.ManagedFolder : file.ManagedFolder;
        if (string.IsNullOrEmpty(locationPath) || folder is null)
        {
            logger.LogTrace("Invalid path or managed folder. Skipping relocation for video file: {Path}. (Video={VideoID},Location={LocationID})", locationPath, file.VideoID, file.ID);
            return;
        }

        if (!RelocateOnImport)
        {
            logger.LogTrace("Auto-Relocation is disabled. Skipping relocation for video file: {Path} (Video={VideoID},Location={LocationID})", locationPath, file.VideoID, file.ID);
            return;
        }

        if (file.ManagedFolder.DropFolderType is DropFolderType.Excluded)
        {
            logger.LogTrace("Not in a drop destination or source. Skipping relocation for video file: {Path}. (Video={VideoID},Location={LocationID})", locationPath, file.VideoID, file.ID);
            return;
        }

        logger.LogTrace("Scheduling relocation for video file: {Path} (Video={VideoID},Location={LocationID})", locationPath, file.VideoID, file.ID);
        var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
        await scheduler.StartJob<RenameMoveFileLocationJob>(b => (b.ManagedFolderID, b.RelativePath) = (file.ManagedFolderID, file.RelativePath), prioritize: prioritize).ConfigureAwait(false);
    }

    /// <summary>
    /// Automatically relocates a file using the specified relocation request or
    /// default settings.
    /// </summary>
    /// <param name="file">The <see cref="VideoLocal_Place"/> to relocate.</param>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing
    /// the details for the relocation operation, or null for default settings.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public async Task<RelocationResponse> AutoRelocateFile(IVideoFile file, AutoRelocateRequest? request = null)
    {
        // Allows calling the method without any parameters.
        var settings = settingsProvider.GetSettings();
        // give defaults from the settings
        request ??= new()
        {
            Move = settings.Plugins.Renamer.MoveOnImport,
            Rename = settings.Plugins.Renamer.RenameOnImport,
            DeleteEmptyDirectories = settings.Plugins.Renamer.MoveOnImport,
            AllowRelocationInsideDestination = settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport,
        };

        if (request is { Preview: true, Pipe: null })
            return RelocationResponse.FromError("Cannot preview without a provided pipe.");
        if (request is { Move: false, Rename: false })
            return RelocationResponse.FromError("Rename and Move are both set to false. Nothing to do.");

        // make sure we can find the file
        var previousLocation = file.Path;
        if (!File.Exists(previousLocation))
            return RelocationResponse.FromError($"Could not find or access the file to move: {file.FileName} ({file.ID})");

        var retryPolicy = Policy
            .HandleResult<(RelocationResponse Response, bool ShouldRetry)>(a =>
            {
                if (!a.Response.Success)
                {
                    logger.LogError(a.Response.Error.Exception, "Error Renaming/Moving File: {Message}", a.Response.Error.Message);
                }
                return a.ShouldRetry;
            })
            .Or<Exception>(e =>
            {
                logger.LogError(e, "Error Renaming/Moving File");
                return false;
            })
            .WaitAndRetryAsync([
                TimeSpan.FromMilliseconds((int)DelayInUse.First),
                TimeSpan.FromMilliseconds((int)DelayInUse.Second),
                TimeSpan.FromMilliseconds((int)DelayInUse.Third),
            ]);

        // Attempt to relocate the file.
        var dropFolder = file.ManagedFolder;
        var oldRelativePath = file.RelativePath;
        var (relocationResult, _) = await retryPolicy.ExecuteAsync(() => AttemptToRelocateFile(file, request)).ConfigureAwait(false);
        if (!relocationResult.Success)
            return relocationResult;

        if (!request.Preview && !string.Equals(relocationResult.AbsolutePath, previousLocation, StringComparison.OrdinalIgnoreCase))
            DispatchFileRelocatedEvent(dropFolder, relocationResult.ManagedFolder, oldRelativePath, relocationResult.RelativePath, file);

        if (request.Preview)
            logger.LogTrace("Resolved to move from {PreviousPath} to {NextPath}.", previousLocation, relocationResult.AbsolutePath);
        else
            logger.LogTrace("Moved from {PreviousPath} to {NextPath}.", previousLocation, relocationResult.AbsolutePath);
        return relocationResult;
    }

    public async Task<RelocationResponse> DirectlyRelocateFile(IVideoFile file, DirectlyRelocateRequest request)
    {
        var dropFolder = file.ManagedFolder;
        var oldRelativePath = file.RelativePath;
        var previousLocation = Path.Join(dropFolder.Path, oldRelativePath);
        try
        {
            // Actually move it.
            var (result, _) = await InternalDirectlyRelocateFile(file, request).ConfigureAwait(false);
            if (!result.Success)
                return result;

            if (!string.Equals(result.AbsolutePath, previousLocation, StringComparison.OrdinalIgnoreCase))
                DispatchFileRelocatedEvent(dropFolder, result.ManagedFolder, oldRelativePath, result.RelativePath, file);

            logger.LogTrace("Moved from {PreviousPath} to {NextPath}.", previousLocation, result.AbsolutePath);
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            logger.LogError(ex, "An error occurred while trying to find a new file name for {FilePath}: {ErrorMessage}", file.Path, errorMessage);
            return RelocationResponse.FromError($"An error occurred while trying to find a new file name for {file.Path}: {errorMessage}", ex);
        }
    }

    #region Relocation Methods | Internal

    /// <summary>
    /// Renames a file using the specified rename request.
    /// </summary>
    /// <param name="place">The <see cref="VideoLocal_Place"/> to rename.</param>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing the
    ///     details for the rename operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the rename operation.</returns>
    private async Task<(RelocationResponse Result, bool ShouldRetry)> AttemptToRelocateFile(IVideoFile place, AutoRelocateRequest request)
    {
        if (request.CancellationToken.IsCancellationRequested)
            return (RelocationResponse.FromError("Cancellation requested before relocation could begin."), false);

        // Just return the existing values if we're going to skip the operation.
        if (request is { Rename: false, Move: false })
            return (RelocationResponse.FromResult(place.ManagedFolder, place.RelativePath, false, false), false);

        // run the renamer and process the result
        RelocationResponse result;
        try
        {
            result = ProcessPipe(place, request.Pipe, request.Move, request.Rename, request.AllowRelocationInsideDestination, request.CancellationToken);
        }
        catch (Exception ex)
        {
            return (RelocationResponse.FromError($"An error occurred while trying to find a new file location for {place.Path}: {ex.Message}", ex), false);
        }

        // Return early if we're only previewing or if it not a success.
        if (request.Preview || !result.Success)
            return (result, false);

        try
        {
            // Actually move it.
            return await InternalDirectlyRelocateFile(place, new()
            {
                CancellationToken = request.CancellationToken,
                DeleteEmptyDirectories = request.DeleteEmptyDirectories,
                AllowRelocationInsideDestination = request.AllowRelocationInsideDestination,
                ManagedFolder = result.ManagedFolder,
                RelativePath = result.RelativePath,
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (RelocationResponse.FromError($"An error occurred while trying to find a new file location for {place.Path}: {ex.Message}", ex), false);
        }
    }

    internal RelocationResponse ProcessPipe(IVideoFile place, IRelocationPipe? pipe = null, bool? move = null, bool? rename = null, bool? allowRelocationInsideDestination = null, CancellationToken? cancellationToken = null)
    {
        var service = (IRelocationService)this;
        var settings = settingsProvider.GetSettings();
        var shouldMove = move ?? settings.Plugins.Renamer.MoveOnImport;
        var shouldRename = rename ?? settings.Plugins.Renamer.RenameOnImport;
        var shouldAllowRelocationInsideDestination = allowRelocationInsideDestination ?? settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport;

        // Make sure the managed folder is reachable.
        var managedFolder = (IManagedFolder?)place.ManagedFolder;
        if (managedFolder is null)
            return RelocationResponse.FromError($"Unable to find managed folder for file with ID {place.VideoID}.");

        // Don't relocate files not in a drop source or drop destination.
        if (managedFolder.DropFolderType is DropFolderType.Excluded)
            return RelocationResponse.FromError("Not relocating file as it is not in a drop source or drop destination.");

        // Or if it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled.
        if (managedFolder.DropFolderType is DropFolderType.Destination && !shouldAllowRelocationInsideDestination)
            return RelocationResponse.FromError("Not relocating file because it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled.");
        if (pipe is null)
        {
            var defaultRenamerName = settings.Plugins.Renamer.DefaultRenamer;
            if (string.IsNullOrWhiteSpace(defaultRenamerName))
                return RelocationResponse.FromError("No default renamer configured and no renamer config given.");

            var defaultPipe = service.GetDefaultPipe();
            if (defaultPipe is null)
                return RelocationResponse.FromError("No default renamer configured and no renamer config given.");

            pipe = defaultPipe;
        }

        if (service.GetProviderInfo(pipe.ProviderID) is not { } providerInfo)
            return RelocationResponse.FromError($"No relocation provider with ID \"{pipe.ProviderID}\" is unavailable or unknown.");

        var videoLocal = place.Video;
        var xrefs = videoLocal.CrossReferences;
        var episodes = xrefs
            .Select(x => x.ShokoEpisode)
            .WhereNotNull()
            .ToList();

        // We don't have all the data yet, so don't try to relocate yet if the provider doesn't support handling incomplete metadata.
        if (xrefs.Count != episodes.Count && !providerInfo.SupportsIncompleteMetadata)
            return RelocationResponse.FromError($"Not enough data to do renaming for the recognized file. Missing metadata for {xrefs.Count - episodes.Count} episodes.");

        // Don't try to relocate if the provider doesn't support unrecognized files and the file is unrecognized.
        if (xrefs.Count == 0 && !providerInfo.SupportsUnrecognized)
            return RelocationResponse.FromError("Configured renamer does not support unrecognized files, and the file is unrecognized.");

        var anime = xrefs
            .DistinctBy(x => x.AnidbAnimeID)
            .Select(x => x.ShokoSeries)
            .WhereNotNull()
            .ToList();
        var groups = anime
            .DistinctBy(a => a.ParentGroupID)
            .Select(a => a.ParentGroup)
            .WhereNotNull()
            .ToList();
        var availableFolders = RepoFactory.ShokoManagedFolder.GetAll()
            .Cast<IManagedFolder>()
            .Where(a => a.DropFolderType != DropFolderType.Excluded)
            .ToList();
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
        var args = new RelocationContext
        {
            CancellationToken = cancellationTokenSource.Token,
            Series = anime,
            File = place,
            Episodes = episodes,
            Groups = groups,
            AvailableFolders = availableFolders,
            MoveEnabled = shouldMove,
            RenameEnabled = shouldRename,
        };
        if (providerInfo.ConfigurationInfo is { } configInfo)
        {
            if (pipe.Configuration is null)
                return RelocationResponse.FromError("Pipe is missing it's required configuration.");

            var instanceInterface = providerInfo.Provider.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRelocationProvider<>))!;
            if (!_genericGetNewPaths.TryGetValue(instanceInterface, out var method))
                _genericGetNewPaths.TryAdd(instanceInterface, method = instanceInterface.GetMethod(nameof(IRelocationProvider.GetPath), BindingFlags.Instance | BindingFlags.Public));
            if (method == null)
                return RelocationResponse.FromError($"Unable to find \"{nameof(IRelocationProvider.GetPath)}\" method on relocation provider \"{instanceInterface.FullName}\".");

            var config = configurationService.Deserialize(configInfo, Encoding.UTF8.GetString(pipe.Configuration!));
            var argsType = typeof(RelocationContext<>).MakeGenericType(configInfo.Type);
            args = (RelocationContext)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, argsType, args, config);

            return ConvertToResponse(place, ProcessPipe((r, a) => (RelocationResult)method.Invoke(r, [a])!, providerInfo.Provider, args, shouldRename, shouldMove), shouldMove, shouldRename);
        }

        return ConvertToResponse(place, ProcessPipe((r, a) => (r).GetPath(a), providerInfo.Provider, args, shouldRename, shouldMove), shouldMove, shouldRename);
    }

    /// <summary>
    /// Un-abstract the relocation result returned from the renamer, and convert it to something easier to work internally for us.
    /// </summary>
    /// <param name="file">Video file location.</param>
    /// <param name="response">Abstract result returned from the renamed.</param>
    /// <param name="shouldMove">Indicates that we should have moved.</param>
    /// <param name="shouldRename">Indicates that we should have renamed.</param>
    /// <returns>An non-abstract relocation result.</returns>
    private static RelocationResponse ConvertToResponse(IVideoFile file, RelocationResult response, bool shouldMove, bool shouldRename)
    {
        if (response.Error is not null)
            return RelocationResponse.FromError(response.Error);

        var newFolder = shouldMove && !response.SkipMove ? response.ManagedFolder! : file.ManagedFolder!;
        var newFileName = shouldRename && !response.SkipRename ? response.FileName! : file.FileName;
        var newRelativeDirectory = shouldMove && !response.SkipMove ? response.Path : Path.GetDirectoryName(file.RelativePath[1..]);
        var newRelativePath = !string.IsNullOrEmpty(newRelativeDirectory) && newRelativeDirectory.Length > 0 ? Path.Combine(newRelativeDirectory, newFileName) : newFileName;
        var newFullPath = Path.Join(newFolder.Path, newRelativePath);
        return RelocationResponse.FromResult(
            newFolder,
            newRelativePath,
            // TODO: Handle file-systems that are or aren't case sensitive.
            !string.Equals(file.FileName, response.FileName, StringComparison.OrdinalIgnoreCase),
            !string.Equals(Path.GetDirectoryName(file.Path), Path.GetDirectoryName(newFullPath), StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// This is called with reflection, so the signature must match the above
    /// </summary>
    /// <param name="func"></param>
    /// <param name="renamer"></param>
    /// <param name="context"></param>
    /// <param name="shouldRename"></param>
    /// <param name="shouldMove"></param>
    /// <returns></returns>
    private RelocationResult ProcessPipe(Func<IRelocationProvider, RelocationContext, RelocationResult> func, IRelocationProvider renamer, RelocationContext context, bool shouldRename,
        bool shouldMove)
    {
        try
        {
            // get filename from plugin
            var result = func(renamer, context);
            if (result.Error is not null) return result;

            context.CancellationToken.ThrowIfCancellationRequested();

            if (shouldRename && !result.SkipRename && (string.IsNullOrWhiteSpace(result.FileName) || result.FileName.StartsWith("*Error:")))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(result.FileName)
                    ? result.FileName[7..].Trim()
                    : $"The renamer \"{renamer.GetType().Name}\" returned a null or empty value for the file name.";
                logger.LogError("An error occurred while trying to find a new file name for {FilePath}: {ErrorMessage}", context.File.Path, errorMessage);
                return new() { Error = new RelocationError(errorMessage) };
            }

            // Normalize file name.
            if (!string.IsNullOrEmpty(result.FileName))
            {
                // Move path from file name to path if it's provided in the file name and not as the Path.
                if (Path.GetDirectoryName(result.FileName) is { } dirName && string.IsNullOrEmpty(result.Path))
                    result.Path = dirName;

                // Ensure file name only contains a name and no path.
                result.FileName = Path.GetFileName(result.FileName).Trim();
            }

            // Replace alt separator with main separator.
            result.Path = result.Path?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();

            // Ensure the path does not have a leading separator.
            if (!string.IsNullOrEmpty(result.Path) && result.Path[0] == Path.DirectorySeparatorChar)
                result.Path = result.Path[1..];

            if (shouldMove && !result.SkipMove && (result.ManagedFolder is null || result.Path is null || result.Path.StartsWith("*Error:")))
            {
                var errorMessage = !string.IsNullOrWhiteSpace(result.Path) && result.Path.StartsWith("*Error:")
                    ? result.Path[7..].Trim()
                    : $"The renamer \"{renamer.GetType().Name}\" could not find a valid destination.";
                logger.LogWarning("An error occurred while trying to find a destination for {FilePath}: {ErrorMessage}", context.File.Path, errorMessage);
                return new() { Error = new RelocationError(errorMessage) };
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return new RelocationResult
            {
                Error = new RelocationError($"Operation canceled by renamer {renamer.GetType().Name}.")
            };
        }
        catch (Exception e)
        {
            return new RelocationResult
            {
                Error = new RelocationError(e.Message, e)
            };
        }
    }

    /// <summary>
    /// Relocates a file directly to the specified location based on the given
    /// request.
    /// </summary>
    /// <param name="file">The <see cref="VideoLocal_Place"/> to relocate.</param>
    /// <param name="request">The <see cref="DirectlyRelocateRequest"/> containing
    /// the details for the relocation operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    private async Task<(RelocationResponse Response, bool ShouldRetry)> InternalDirectlyRelocateFile(IVideoFile file, DirectlyRelocateRequest request)
    {
        if (file is not VideoLocal_Place place)
            return (RelocationResponse.FromError(
                "Invalid request object."
            ), false);

        if (request.CancellationToken.IsCancellationRequested)
            return (RelocationResponse.FromError(
                "Cancellation requested before relocation could begin."
            ), false);

        if (request.ManagedFolder is null || string.IsNullOrWhiteSpace(request.RelativePath))
            return (RelocationResponse.FromError(
                "Invalid request object, managed folder, or relative path."
            ), false);

        if (place.VideoLocal is not { } video)
            return (RelocationResponse.FromError(
                "Could not find the associated video for the video file location."
            ), false);

        // Sanitize relative path and reject paths leading to outside the managed folder.
        var fullPath = Path.GetFullPath(Path.Combine(request.ManagedFolder.Path, request.RelativePath));
        if (!fullPath.StartsWith(request.ManagedFolder.Path, StringComparison.OrdinalIgnoreCase))
            return (RelocationResponse.FromError(
                "The provided relative path leads outside the managed folder."
            ), false);

        var dropFolder = place.ManagedFolder;
        if (dropFolder is null)
            return (RelocationResponse.FromError(
                "Unable to find managed folder for the video file location."
            ), false);

        var oldRelativePath = place.RelativePath;
        var oldFullPath = string.IsNullOrEmpty(oldRelativePath) ? null : Path.Combine(dropFolder.Path, oldRelativePath);
        if (string.IsNullOrWhiteSpace(oldRelativePath) || string.IsNullOrWhiteSpace(oldFullPath))
            return (RelocationResponse.FromError(
                "Unable to resolve the full path for the video file location."
            ), false);

        // Don't relocate files not in a drop source or drop destination.
        if (dropFolder.DropFolderType is DropFolderType.Excluded)
            return (RelocationResponse.FromError(
                "Unable to relocate the video file as it is not in a drop source or drop destination."
            ), false);

        // Or if it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled.
        if (dropFolder.DropFolderType is DropFolderType.Destination && !request.AllowRelocationInsideDestination)
            return (RelocationResponse.FromError(
                "Not relocating file because it's in a drop destination not also marked as a drop source and relocating inside destinations is disabled."
            ), false);

        // this can happen due to file locks, so retry in a while.
        if (!File.Exists(oldFullPath))
            return (RelocationResponse.FromError(
                "Could not find or access the video file in the file system!"
            ), true);

        var newRelativePath = Path.GetRelativePath(request.ManagedFolder.Path, fullPath);
        var newFolderPath = Path.GetDirectoryName(newRelativePath);
        var newFullPath = Path.Combine(request.ManagedFolder.Path, newRelativePath);
        var newFileName = Path.GetFileName(newRelativePath);
        var renamed = !string.Equals(Path.GetFileName(oldRelativePath), newFileName, StringComparison.OrdinalIgnoreCase);
        var moved = !string.Equals(Path.GetDirectoryName(oldFullPath), Path.GetDirectoryName(newFullPath), StringComparison.OrdinalIgnoreCase);

        // Check if we're attempting to move the file onto itself.
        if (string.Equals(newFullPath, oldFullPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogTrace("Resolved to relocate {FilePath} onto itself. Nothing to do.", newFullPath);
            return (RelocationResponse.FromResult(request.ManagedFolder, newRelativePath), false);
        }

        // Check if the managed folder can accept the file.
        if (
            request.ManagedFolder.ID != place.ManagedFolderID &&
            request.ManagedFolder.AvailableFreeSpace is not -2 &&
            request.ManagedFolder.AvailableFreeSpace < video.FileSize
        )
            return (RelocationResponse.FromError(
                "The managed folder cannot accept the file due to insufficient space available!"
            ), false);

        var destFullTree = string.IsNullOrEmpty(newFolderPath)
            ? request.ManagedFolder.Path
            : Path.Combine(request.ManagedFolder.Path, newFolderPath);
        if (!Directory.Exists(destFullTree))
        {
            fileWatcherService.AddFileWatcherExclusion(destFullTree);
            try
            {
                Directory.CreateDirectory(destFullTree);
            }
            catch (Exception ex)
            {
                return (RelocationResponse.FromError(
                    $"An unexpected error occurred while trying to create the new destination tree; {ex.Message}"
                , ex), false);
            }
            finally
            {
                fileWatcherService.RemoveFileWatcherExclusion(destFullTree);
            }
        }

        var sourceFile = new FileInfo(oldFullPath);
        var destVideoLocalPlace = videoLocalPlace.GetByRelativePathAndManagedFolderID(newRelativePath, request.ManagedFolder.ID);
        var relocatedFile = false;
        if (File.Exists(newFullPath))
        {
            // A file with the same name exists at the destination.
            logger.LogTrace("A file already exists at the new location, checking it for duplicate…");
            var destVideoLocal = destVideoLocalPlace?.VideoLocal;
            if (destVideoLocalPlace is null || destVideoLocal is null)
                return (RelocationResponse.FromError(
                    "The existing file at the new location is not a known video file. Not moving."
                ), false);

            if (destVideoLocal.Hash == video.Hash)
                return (RelocationResponse.FromError(
                    "Not moving video file as it already exists at the new location."
                ), false);

            // Not a dupe, don't delete it
            logger.LogTrace("A different file already exists at the new location, checking it for version and group");
            if (destVideoLocal.ReleaseInfo is not { } destinationExistingReleaseInfo)
                return (RelocationResponse.FromError(
                    "The existing file at the new location does not have release info. Not moving."
                ), false);

            if (video.ReleaseInfo is not { } releaseInfo)
                return (RelocationResponse.FromError(
                    "The file does not have release info. Not moving."
                ), false);

            if (destinationExistingReleaseInfo.GroupID == releaseInfo.GroupID &&
                destinationExistingReleaseInfo.GroupSource == releaseInfo.GroupSource &&
                destinationExistingReleaseInfo.Version < releaseInfo.Version)
            {
                if (request.CancellationToken.IsCancellationRequested)
                    return (RelocationResponse.FromError(
                        "Cancellation requested before relocation took place."
                    ), false);

                // This is a V2 replacing a V1 with the same name.
                // Normally we'd let the Multiple Files Utility handle it, but let's just delete the V1
                logger.LogInformation("The existing file is a V1 from the same group. Replacing it.");

                // Delete the destination
                await _videoLocalPlaceService.RemoveRecordAndDeletePhysicalFile(destVideoLocalPlace);

                // Move
                fileWatcherService.AddFileWatcherExclusion(oldFullPath);
                fileWatcherService.AddFileWatcherExclusion(newFullPath);
                logger.LogInformation("Moving file from {PreviousPath} to {NextPath}", oldFullPath, newFullPath);
                try
                {
                    sourceFile.MoveTo(newFullPath);
                    SetLinuxPermissions(newFullPath);
                }
                catch (Exception ex)
                {
                    return (RelocationResponse.FromError(
                        $"Unable to relocate video file! Error: {ex.Message}"
                    , ex), true);
                }
                finally
                {
                    fileWatcherService.RemoveFileWatcherExclusion(oldFullPath);
                    fileWatcherService.RemoveFileWatcherExclusion(newFullPath);
                }

                place.ManagedFolderID = request.ManagedFolder.ID;
                place.RelativePath = newRelativePath;
                videoLocalPlace.Save(place);
                relocatedFile = true;
            }
        }
        else
        {
            if (request.CancellationToken.IsCancellationRequested)
                return (RelocationResponse.FromError(
                    "Cancellation requested before relocation took place."
                ), false);

            if (destVideoLocalPlace is not null)
            {
                logger.LogTrace("An entry already exists for the new location at {NewPath} but no physical file resides there. Removing the entry.", newFullPath);
                await _videoLocalPlaceService.RemoveRecord(destVideoLocalPlace);
            }

            // Move
            fileWatcherService.AddFileWatcherExclusion(oldFullPath);
            fileWatcherService.AddFileWatcherExclusion(newFullPath);
            logger.LogInformation("Moving file from {PreviousPath} to {NextPath}", oldFullPath, newFullPath);
            try
            {
                sourceFile.MoveTo(newFullPath);
                SetLinuxPermissions(newFullPath);
            }
            catch (Exception ex)
            {
                return (RelocationResponse.FromError(
                    $"Unable to relocate video file! Error: {ex.Message}"
                , ex), true);
            }
            finally
            {
                fileWatcherService.RemoveFileWatcherExclusion(oldFullPath);
                fileWatcherService.RemoveFileWatcherExclusion(newFullPath);
            }

            place.ManagedFolderID = request.ManagedFolder.ID;
            place.RelativePath = newRelativePath;
            videoLocalPlace.Save(place);
            relocatedFile = true;
        }

        if (renamed)
        {
            // Add a new or update an existing lookup entry.
            var existingEntries = fileNameHash.GetByHash(video.Hash);
            if (!existingEntries.Any(a => a.FileName.Equals(newFileName)))
            {
                var hash = fileNameHash.GetByFileNameAndSize(newFileName, video.FileSize).FirstOrDefault() ??
                    new() { FileName = newFileName, FileSize = video.FileSize };
                hash.DateTimeUpdated = DateTime.Now;
                hash.Hash = video.Hash;
                fileNameHash.Save(hash);
            }
        }

        // Move the external subtitles.
        MoveExternalSubtitles(newFullPath, oldFullPath);

        if (relocatedFile && request.DeleteEmptyDirectories)
            _videoLocalPlaceService.RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(oldFullPath), dropFolder.Path);

        return (RelocationResponse.FromResult(request.ManagedFolder, newRelativePath, moved, renamed), false);
    }

    private void SetLinuxPermissions(string path)
    {
        var settings = settingsProvider.GetSettings();

        // Set the linux permissions now if we're not previewing the result.
        try
        {
            LinuxFS.SetLinuxPermissions(path, settings.Linux.UID, settings.Linux.GID, settings.Linux.Permission);
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Unable to set permissions ({Uid}:{Gid} {Permission}) on file {FileName}: Access Denied", settings.Linux.UID,
                settings.Linux.GID, settings.Linux.Permission, Path.GetFileName(path));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error setting Linux Permissions: {Ex}", e);
        }
    }

    private void MoveExternalSubtitles(string newFullServerPath, string oldFullServerPath)
    {
        try
        {
            var oldParent = Path.GetDirectoryName(oldFullServerPath);
            var newParent = Path.GetDirectoryName(newFullServerPath);
            var oldFileName = Path.GetFileNameWithoutExtension(oldFullServerPath);
            var newFileName = Path.GetFileNameWithoutExtension(newFullServerPath);
            if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(oldParent) ||
                string.IsNullOrEmpty(oldFileName) || string.IsNullOrEmpty(newFileName))
                return;

            var textStreams = SubtitleHelper.GetSubtitleStreams(oldFullServerPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename))
                    continue;

                var subPath = Path.Combine(oldParent, subtitleFile.Filename);
                var subExtraPart = subtitleFile.Filename[oldFileName.Length..];
                var subFile = new FileInfo(subPath);
                if (!subFile.Exists)
                {
                    logger.LogError("Unable to rename external subtitle file {SubtitleFile}. Cannot access the file.", subPath);
                    continue;
                }

                var newSubPath = Path.Combine(newParent, newFileName + subExtraPart);
                if (string.Equals(subPath, newSubPath, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Attempting to move subtitle file onto itself. Skipping. Path: {FilePath} to {FilePath}", subPath, newSubPath);
                    continue;
                }

                if (File.Exists(newSubPath))
                {
                    try
                    {
                        File.Delete(newSubPath);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Unable to DELETE file: {SubtitleFile}\n{ErrorMessage}", subPath, e.Message);
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Unable to MOVE file: {PreviousFile} to {NextPath}\n{ErrorMessage}", subPath, newSubPath, e.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while trying to move an external subtitle file for {FilePath}\n{ErrorMessage}", oldFullServerPath, ex.Message);
        }
    }

    private void DispatchFileRelocatedEvent(IManagedFolder oldFolder, IManagedFolder newFolder, string oldPath, string newPath, IVideoFile vlp)
    {
        var vl = vlp.Video!;
        var xrefs = vl.CrossReferences;
        var episodes = xrefs
            .Select(x => x.ShokoEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnidbAnimeID)
            .Select(x => x.ShokoSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.ParentGroupID)
            .Select(a => a.ParentGroup)
            .WhereNotNull()
            .ToList();
        FileRelocated?.Invoke(null, new(newPath, newFolder, oldPath, oldFolder, vlp, vl, episodes, series, groups));
    }

    #endregion

    #endregion

    #region Utility Methods

    public IManagedFolder? GetFirstDestinationWithSpace(RelocationContext context)
    {
        if (settingsProvider.GetSettings().Import.SkipDiskSpaceChecks)
            return context.AvailableFolders.FirstOrDefault(folder => folder.DropFolderType.HasFlag(DropFolderType.Destination));

        return context.AvailableFolders.Where(folder => folder.DropFolderType.HasFlag(DropFolderType.Destination) && Directory.Exists(folder.Path))
            .FirstOrDefault(folder => ManagedFolderHasSpace(folder, context.File));
    }

    public bool ManagedFolderHasSpace(IManagedFolder folder, IVideoFile file)
    {
        return folder.ID == file.ManagedFolderID || folder.AvailableFreeSpace is -2 || folder.AvailableFreeSpace >= file.Size;
    }

    public (IManagedFolder ManagedFolder, string RelativePath)? GetExistingSeriesLocationWithSpace(RelocationContext args)
    {
        var series = args.Series.Select(s => s.AnidbAnime).FirstOrDefault();
        if (series is null)
            return null;

        // sort the episodes by air date, so that we will move the file to the location of the latest episode
        var allEps = series.Episodes
            .OrderByDescending(a => a.AirDate ?? DateTime.MinValue)
            .ToList();

        var skipDiskSpaceChecks = settingsProvider.GetSettings().Import.SkipDiskSpaceChecks;
        foreach (var ep in allEps)
        {
            var videoList = ep.VideoList;
            // check if this episode belongs to more than one anime
            // if it does, we will ignore it
            if (videoList.SelectMany(v => v.Series).DistinctBy(s => s.AnidbAnimeID).Count() > 1)
                continue;

            foreach (var vid in videoList)
            {
                if (vid.ED2K == args.File.Video.ED2K) continue;

                var place = vid.Files.FirstOrDefault(b =>
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    b.ManagedFolder is not null &&
                    !b.ManagedFolder.DropFolderType.HasFlag(DropFolderType.Source) &&
                    !string.IsNullOrWhiteSpace(b.RelativePath));
                if (place is null) continue;

                var placeFld = place.ManagedFolder;

                // check space
                if (!skipDiskSpaceChecks && !ManagedFolderHasSpace(placeFld, args.File))
                    continue;

                var placeDir = Path.GetDirectoryName(place.Path);
                if (placeDir is null)
                    continue;
                // ensure we aren't moving to the current directory
                if (placeDir.Equals(Path.GetDirectoryName(args.File.Path), StringComparison.InvariantCultureIgnoreCase))
                    continue;

                return (placeFld, Path.GetRelativePath(placeFld.Path, placeDir));
            }
        }

        return null;
    }

    #endregion

    #region ID Helpers

    private Guid GetID(Type providerType)
        => _loaded && pluginManager.GetPluginInfo(providerType.Assembly) is { } pluginInfo
            ? GetID(providerType, pluginInfo)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"RelocationProvider={type.FullName!}", pluginInfo.ID);

    #endregion ID Helpers
}
