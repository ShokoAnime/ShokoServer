using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.FileHelper;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

using IShokoEventHandler = Shoko.Plugin.Abstractions.IShokoEventHandler;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractVideoHashingService : IVideoHashingService
{
    private readonly ILogger<AbstractVideoHashingService> _logger;

    private readonly ConfigurationProvider<VideoHashingServiceSettings> _configurationProvider;

    private readonly IShokoEventHandler _shokoEventHandler;

    private readonly IPluginManager _pluginManager;

    private Dictionary<Guid, HashProviderInfo> _hashProviderInfos = [];

    private readonly object _lock = new();

    private bool _loaded = false;

    private Guid _coreProviderID = Guid.Empty;

    public event EventHandler<FileEventArgs>? FileHashed;

    public event EventHandler? ProvidersUpdated;

    public event EventHandler? Ready;

    public bool ParallelMode
    {
        get => _configurationProvider.Load().ParallelMode;
        set
        {
            var config = _configurationProvider.Load();
            if (config.ParallelMode == value)
                return;

            config.ParallelMode = value;
            _configurationProvider.Save(config);
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlySet<string> AllAvailableHashTypes => GetAvailableProviders()
        .SelectMany(p => p.Provider.AvailableHashTypes)
        .ToHashSet();

    public IReadOnlySet<string> AllEnabledHashTypes => GetAvailableProviders(onlyEnabled: true)
        .SelectMany(p => p.EnabledHashTypes)
        .ToHashSet();

    public AbstractVideoHashingService(
        ILogger<AbstractVideoHashingService> logger,
        ConfigurationProvider<VideoHashingServiceSettings> configurationProvider,
        IPluginManager pluginManager,
        IShokoEventHandler shokoEventHandler
    )
    {
        _logger = logger;
        _configurationProvider = configurationProvider;
        _shokoEventHandler = shokoEventHandler;
        _pluginManager = pluginManager;

        _shokoEventHandler.FileHashed += OnFileHashed;
    }

    ~AbstractVideoHashingService()
    {
        _shokoEventHandler.FileHashed -= OnFileHashed;
    }

    private void OnFileHashed(object? sender, FileEventArgs args) => FileHashed?.Invoke(this, args);

    public void AddParts(IEnumerable<IHashProvider> providers)
    {
        if (_loaded) return;
        _loaded = true;

        _logger.LogInformation("Initializing service.");

        lock (_lock)
        {
            var config = _configurationProvider.Load();
            var order = config.Priority;
            var enabled = config.EnabledHashes;
            _coreProviderID = GetID(typeof(CoreHashProvider), _pluginManager.GetPluginInfo(typeof(CorePlugin))!);
            _hashProviderInfos = providers
                .Select((provider, priority) =>
                {
                    var pluginInfo = _pluginManager.GetPluginInfo(
                        Loader.GetTypes<IPlugin>(provider.GetType().Assembly)
                            .First(t => _pluginManager.GetPluginInfo(t) is not null)
                    )!;
                    var id = GetID(provider.GetType(), pluginInfo);
                    var contextualType = provider.GetType().ToContextualType();
                    var enabledHashes = enabled.TryGetValue(id, out var h) ? h : id == _coreProviderID ? ["ED2K"] : [];
                    var description = contextualType.GetDescription();
                    return new HashProviderInfo()
                    {
                        ID = id,
                        Description = description,
                        Provider = provider,
                        PluginInfo = pluginInfo,
                        EnabledHashTypes = enabledHashes,
                        Priority = priority,
                    };
                })
                .OrderBy(p => order.IndexOf(p.ID) is -1)
                .ThenBy(p => order.IndexOf(p.ID))
                .ThenBy(p => p.ID)
                .ToDictionary(info => info.ID);
        }

        UpdateProviders(false);

        _logger.LogInformation("Loaded {ProviderCount} providers.", _hashProviderInfos.Count);

        Ready?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<HashProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
        => _hashProviderInfos.Values
            .Where(info => !onlyEnabled || info.EnabledHashTypes.Count > 0)
            .OrderBy(info => info.Priority)
            // Create a copy so that we don't affect the original entries
            .Select(info => new HashProviderInfo()
            {
                ID = info.ID,
                Description = info.Description,
                Provider = info.Provider,
                PluginInfo = info.PluginInfo,
                EnabledHashTypes = info.EnabledHashTypes.ToHashSet(),
                Priority = info.Priority
            });

    public IReadOnlyList<HashProviderInfo> GetProviderInfo(IPlugin plugin)
        => _hashProviderInfos.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Provider.Name)
            .ThenBy(info => info.ID)
            // Create a copy so that we don't affect the original entries
            .Select(info => new HashProviderInfo()
            {
                ID = info.ID,
                Description = info.Description,
                Provider = info.Provider,
                PluginInfo = info.PluginInfo,
                EnabledHashTypes = info.EnabledHashTypes.ToHashSet(),
                Priority = info.Priority
            })
            .ToList();

    public HashProviderInfo GetProviderInfo(IHashProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(provider.GetType()))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public HashProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IHashProvider
    {
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public HashProviderInfo? GetProviderInfo(Guid providerID)
        => _hashProviderInfos?.TryGetValue(providerID, out var providerInfo) ?? false
            // Create a copy so that we don't affect the original entry
            ? new()
            {
                ID = providerInfo.ID,
                Description = providerInfo.Description,
                Provider = providerInfo.Provider,
                PluginInfo = providerInfo.PluginInfo,
                EnabledHashTypes = providerInfo.EnabledHashTypes.ToHashSet(),
                Priority = providerInfo.Priority,
            }
            : null;

    public void UpdateProviders(params HashProviderInfo[] providers)
        => UpdateProviders(true, providers);

    private void UpdateProviders(bool fireEvent, params HashProviderInfo[] providers)
    {
        if (!_loaded)
            return;

        var existingProviders = GetAvailableProviders().ToList();
        foreach (var providerInfo in providers)
        {
            var wantedIndex = providerInfo.Priority;
            var existingIndex = existingProviders.FindIndex(p => p.Provider == providerInfo.Provider);
            if (existingIndex is -1)
                continue;

            // Enable or disable provider.
            providerInfo.EnabledHashTypes.IntersectWith(providerInfo.Provider.AvailableHashTypes);
            if (!providerInfo.EnabledHashTypes.SetEquals(existingProviders[existingIndex].EnabledHashTypes))
                existingProviders[existingIndex].EnabledHashTypes = providerInfo.EnabledHashTypes;

            // Move index.
            if (wantedIndex != existingIndex)
            {
                var pI = existingProviders[existingIndex];
                existingProviders.RemoveAt(existingIndex);
                if (wantedIndex < 0)
                    existingProviders.Add(pI);
                else
                    existingProviders.Insert(wantedIndex, pI);
            }
        }

        var changed = false;
        var config = _configurationProvider.Load();
        var priority = existingProviders.Select(pI => pI.ID).ToList();
        if (!config.Priority.SequenceEqual(priority))
        {
            config.Priority = priority;
            changed = true;
        }

        var enabled = existingProviders.ToDictionary(p => p.ID, p => p.EnabledHashTypes);
        // Ensure we at least have 1 ED2K hash provider at all times.
        if (!enabled.Any(kp => kp.Value.Contains("ED2K")))
            enabled[_coreProviderID].Add("ED2K");
        // Remove any providers with no hashes.
        enabled = enabled
            .Where(kp => kp.Value.Count > 0)
            .ToDictionary(kp => kp.Key, kp => kp.Value);
        if (!config.EnabledHashes.SequenceEqual(enabled))
        {
            config.EnabledHashes = enabled;
            changed = true;
        }

        if (changed)
        {
            lock (_lock)
            {
                _hashProviderInfos = existingProviders
                    // Create a copy so that we don't affect the original entry
                    .Select(info => new HashProviderInfo()
                    {
                        ID = info.ID,
                        Description = info.Description,
                        Provider = info.Provider,
                        PluginInfo = info.PluginInfo,
                        EnabledHashTypes = info.EnabledHashTypes.ToHashSet(),
                        Priority = info.Priority,
                    })
                    .ToDictionary(info => info.ID);
            }
            _configurationProvider.Save(config);
            if (fireEvent)
                ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<IReadOnlyList<IHashDigest>> GetHashesForFile(FileInfo fileInfo, IReadOnlyList<IHashDigest>? existingHashes = null, CancellationToken cancellationToken = default)
    {
        if (!_loaded)
            return [];

        existingHashes ??= [];
        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        return ParallelMode ?
            await GetHashesForFileParallel(fileInfo, existingHashes, providers, cancellationToken) :
            await GetHashesForFileSequential(fileInfo, existingHashes, providers, cancellationToken);
    }

    private async Task<IReadOnlyList<IHashDigest>> GetHashesForFileSequential(FileInfo fileInfo, IReadOnlyList<IHashDigest> existingHashes, IReadOnlyList<HashProviderInfo> providers, CancellationToken cancellationToken)
    {
        var allHashes = new ConcurrentBag<(HashProviderInfo, IReadOnlyList<IHashDigest>)>() { };
        await Task.WhenAll(providers.Select(providerInfo => Task.Run(async () =>
        {
            var newHashes = await GetHashesForFileAndProvider(fileInfo, providerInfo, existingHashes, cancellationToken);
            allHashes.Add((providerInfo, newHashes));
        }, cancellationToken)));
        return allHashes.ToArray()
            .OrderBy(tuple => tuple.Item1.Priority)
            .Select(tuple => tuple.Item2)
            .Prepend(existingHashes)
            .SelectMany(h => h)
            .Distinct()
            .Order()
            .ToList();
    }

    private async Task<IReadOnlyList<IHashDigest>> GetHashesForFileParallel(FileInfo fileInfo, IReadOnlyList<IHashDigest> existingHashes, IReadOnlyList<HashProviderInfo> providers, CancellationToken cancellationToken)
    {
        var allHashes = new List<IHashDigest>(existingHashes);
        foreach (var providerInfo in providers)
        {
            var newHashes = await GetHashesForFileAndProvider(fileInfo, providerInfo, existingHashes, cancellationToken);
            allHashes.AddRange(newHashes);
            if (cancellationToken.IsCancellationRequested)
                break;
        }

        return allHashes;
    }

    private async Task<IReadOnlyList<IHashDigest>> GetHashesForFileAndProvider(FileInfo fileInfo, HashProviderInfo providerInfo, IReadOnlyList<IHashDigest> existingHashes, CancellationToken cancellationToken)
    {
        var request = new HashingRequest()
        {
            EnabledHashTypes = providerInfo.EnabledHashTypes,
            ExistingHashes = existingHashes,
            File = fileInfo,
        };
        var hashes = await providerInfo.Provider.GetHashesForVideo(request, cancellationToken);
        return hashes
            .Where(h => providerInfo.EnabledHashTypes.Contains(h.Type))
            .ToList();
    }

    private Guid GetID(Type providerType)
        => _loaded && Loader.GetTypes<IPlugin>(providerType.Assembly).FirstOrDefault(t => _pluginManager.GetPluginInfo(t) is not null) is { } pluginType
            ? GetID(providerType, _pluginManager.GetPluginInfo(pluginType)!)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"HashProvider={type.FullName!}", pluginInfo.ID);
}
