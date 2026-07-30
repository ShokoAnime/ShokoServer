using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class VideoStreamPipelineService(
    ILogger<VideoStreamPipelineService> logger,
    IConfigurationService configurationService,
    ConfigurationProvider<VideoStreamPipelineSettings> configurationProvider,
    IPluginManager pluginManager
) : IVideoStreamPipelineService
{
    private readonly Lock _lock = new();

    private Dictionary<Guid, VideoStreamTransformInfo> _transformInfos = [];

    private Dictionary<Guid, PlaybackObserverInfo> _observerInfos = [];

    private bool _transformsLoaded;

    private bool _observersLoaded;

    public event EventHandler? TransformsUpdated;

    public event EventHandler? ObserversUpdated;

    #region Transforms

    public void AddTransformParts(IEnumerable<IVideoStreamTransform> transforms)
    {
        if (_transformsLoaded) return;
        _transformsLoaded = true;

        lock (_lock)
        {
            var config = configurationProvider.Load();
            var order = config.TransformPriority;
            var enabled = config.TransformEnabled;
            _transformInfos = transforms
                .Select(transform =>
                {
                    var transformType = transform.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(transformType.Assembly)!;
                    var id = GetTransformID(transformType, pluginInfo);
                    var isEnabled = enabled.TryGetValue(id, out var enabledValue) && enabledValue;
                    var description = transform.Description?.CleanDescription() ?? string.Empty;
                    var configurationType = transformType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVideoStreamTransform<>))
                        ?.GetGenericArguments()[0];
                    var configurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType);
                    return new VideoStreamTransformInfo()
                    {
                        ID = id,
                        Version = transform.Version,
                        Name = transform.Name,
                        Description = description,
                        Transform = transform,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                        Enabled = isEnabled,
                        Priority = -1,
                    };
                })
                .OrderBy(t => order.IndexOf(t.ID) is -1)
                .ThenBy(t => order.IndexOf(t.ID))
                .ThenBy(t => t.ID)
                .Select((info, priority) => CopyTransform(info, priority: priority))
                .ToDictionary(info => info.ID);
        }

        UpdateTransforms(fireEvent: false, transforms: []);

        logger.LogInformation("Loaded {Count} video stream transforms.", _transformInfos.Count);
    }

    public IEnumerable<VideoStreamTransformInfo> GetAvailableTransforms(bool onlyEnabled = false)
        => _transformInfos.Values
            .Where(info => !onlyEnabled || info.Enabled)
            .OrderBy(info => info.Priority)
            .Select(info => CopyTransform(info));

    public IEnumerable<VideoStreamTransformInfo> GetApplicableTransforms(IVideo video, VideoStreamTransformContext context, bool onlyEnabled = true)
        => GetAvailableTransforms(onlyEnabled)
            .Where(info => info.Transform.SupportsVideo(video, context));

    public VideoStreamTransformInfo? SelectTransform(IVideo video, VideoStreamTransformContext context, Guid? explicitTransformId = null)
    {
        if (explicitTransformId is { } id)
        {
            var info = GetTransformInfo(id);
            return info is { Enabled: true } && info.Transform.SupportsVideo(video, context) ? info : null;
        }

        return GetApplicableTransforms(video, context, onlyEnabled: true).FirstOrDefault();
    }

    public VideoStreamTransformInfo? GetTransformInfo(Guid transformID)
        => _transformInfos.TryGetValue(transformID, out var info) ? CopyTransform(info) : null;

    public void UpdateTransforms(params VideoStreamTransformInfo[] transforms)
        => UpdateTransforms(fireEvent: true, transforms: transforms);

    private void UpdateTransforms(bool fireEvent, params VideoStreamTransformInfo[] transforms)
    {
        if (!_transformsLoaded)
            return;

        var existing = GetAvailableTransforms().ToList();
        foreach (var transformInfo in transforms)
        {
            var wantedIndex = transformInfo.Priority;
            var existingIndex = existing.FindIndex(t => t.ID == transformInfo.ID);
            if (existingIndex is -1)
                continue;

            if (transformInfo.Enabled != existing[existingIndex].Enabled)
                existing[existingIndex].Enabled = transformInfo.Enabled;

            if (wantedIndex != existingIndex)
            {
                var t = existing[existingIndex];
                existing.RemoveAt(existingIndex);
                if (wantedIndex < 0)
                    existing.Add(t);
                else
                    existing.Insert(wantedIndex, t);
            }
        }

        var changed = false;
        var config = configurationProvider.Load();
        var priority = existing.Select(t => t.ID).ToList();
        if (config.TransformPriority.Count != priority.Count || config.TransformPriority.Select((p, i) => (p, i)).Any(tuple => priority[tuple.i] != tuple.p))
        {
            config.TransformPriority = priority;
            changed = true;
        }

        var enabled = existing.OrderBy(t => t.ID).ToDictionary(t => t.ID, t => t.Enabled);
        if (config.TransformEnabled.Count != enabled.Count || !config.TransformEnabled.All(tuple => enabled.TryGetValue(tuple.Key, out var value) && value == tuple.Value))
        {
            config.TransformEnabled = enabled;
            changed = true;
        }

        if (changed)
        {
            lock (_lock)
            {
                _transformInfos = existing
                    .Select(info => CopyTransform(info, priority: priority.IndexOf(info.ID)))
                    .ToDictionary(info => info.ID);
            }
            configurationProvider.Save(config);
            if (fireEvent)
                TransformsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private static VideoStreamTransformInfo CopyTransform(VideoStreamTransformInfo info, int? priority = null) => new()
    {
        ID = info.ID,
        Version = info.Version,
        Name = info.Name,
        Description = info.Description,
        Transform = info.Transform,
        ConfigurationInfo = info.ConfigurationInfo,
        PluginInfo = info.PluginInfo,
        Enabled = info.Enabled,
        Priority = priority ?? info.Priority,
    };

    private Guid GetTransformID(Type transformType)
        => _transformsLoaded && pluginManager.GetPluginInfo(transformType.Assembly) is { } pluginInfo
            ? GetTransformID(transformType, pluginInfo)
            : Guid.Empty;

    private static Guid GetTransformID(Type type, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"VideoStreamTransform={type.FullName!}", pluginInfo.ID);

    #endregion Transforms

    #region Observers

    public void AddObserverParts(IEnumerable<IPlaybackObserver> observers)
    {
        if (_observersLoaded) return;
        _observersLoaded = true;

        lock (_lock)
        {
            var config = configurationProvider.Load();
            var enabled = config.ObserverEnabled;
            _observerInfos = observers
                .Select(observer =>
                {
                    var observerType = observer.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(observerType.Assembly)!;
                    var id = GetObserverID(observerType, pluginInfo);
                    var isEnabled = enabled.TryGetValue(id, out var enabledValue) && enabledValue;
                    var description = observer.Description?.CleanDescription() ?? string.Empty;
                    return new PlaybackObserverInfo()
                    {
                        ID = id,
                        Version = observer.GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0),
                        Name = observer.Name,
                        Description = description,
                        Observer = observer,
                        ConfigurationInfo = null,
                        PluginInfo = pluginInfo,
                        Enabled = isEnabled,
                    };
                })
                .ToDictionary(info => info.ID);
        }

        logger.LogInformation("Loaded {Count} playback observers.", _observerInfos.Count);
    }

    public IEnumerable<PlaybackObserverInfo> GetAvailableObservers(bool onlyEnabled = false)
        => _observerInfos.Values
            .Where(info => !onlyEnabled || info.Enabled)
            .Select(info => CopyObserver(info));

    public PlaybackObserverInfo? GetObserverInfo(Guid observerID)
        => _observerInfos.TryGetValue(observerID, out var info) ? CopyObserver(info) : null;

    public void UpdateObservers(params PlaybackObserverInfo[] observers)
    {
        if (!_observersLoaded)
            return;

        var changed = false;
        var config = configurationProvider.Load();
        lock (_lock)
        {
            foreach (var observerInfo in observers)
            {
                if (!_observerInfos.TryGetValue(observerInfo.ID, out var existing))
                    continue;

                if (existing.Enabled == observerInfo.Enabled)
                    continue;

                existing.Enabled = observerInfo.Enabled;
                changed = true;
            }

            if (changed)
            {
                config.ObserverEnabled = _observerInfos.Values.OrderBy(o => o.ID).ToDictionary(o => o.ID, o => o.Enabled);
            }
        }

        if (changed)
        {
            configurationProvider.Save(config);
            ObserversUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task NotifyPlaybackProgress(PlaybackProgressContext context)
    {
        var observers = GetAvailableObservers(onlyEnabled: true).ToList();
        if (observers.Count == 0)
            return;

        foreach (var observerInfo in observers)
        {
            try
            {
                await observerInfo.Observer.OnPlaybackProgress(context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Playback observer \"{Name}\" threw an exception while processing playback progress.", observerInfo.Name);
            }
        }
    }

    private static PlaybackObserverInfo CopyObserver(PlaybackObserverInfo info) => new()
    {
        ID = info.ID,
        Version = info.Version,
        Name = info.Name,
        Description = info.Description,
        Observer = info.Observer,
        ConfigurationInfo = info.ConfigurationInfo,
        PluginInfo = info.PluginInfo,
        Enabled = info.Enabled,
    };

    private Guid GetObserverID(Type observerType)
        => _observersLoaded && pluginManager.GetPluginInfo(observerType.Assembly) is { } pluginInfo
            ? GetObserverID(observerType, pluginInfo)
            : Guid.Empty;

    private static Guid GetObserverID(Type type, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"PlaybackObserver={type.FullName!}", pluginInfo.ID);

    #endregion Observers
}
