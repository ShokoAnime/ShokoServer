
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.FileHelper;
using Shoko.Server.Settings;

using IShokoEventHandler = Shoko.Plugin.Abstractions.IShokoEventHandler;

namespace Shoko.Server.Services;

public class AbstractVideoHashingService : IVideoHashingService
{
    private IServerSettings _settings => _settingsProvider.GetSettings();

    private readonly ISettingsProvider _settingsProvider;

    private readonly IShokoEventHandler _shokoEventHandler;

    private Dictionary<Guid, IHashProvider>? _hashProviders = null;

    private readonly Guid _coreProviderID = GetID(typeof(CoreHashProvider));

    public event EventHandler<FileEventArgs>? FileHashed;

    public event EventHandler? ProvidersUpdated;

    public bool ParallelMode
    {
        get => _settings.Plugins.HashingProviders.ParallelMode;
        set
        {
            if (_settings.Plugins.HashingProviders.ParallelMode == value)
                return;

            _settings.Plugins.HashingProviders.ParallelMode = value;
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlySet<string> AllAvailableHashTypes => GetAvailableProviders()
        .SelectMany(p => p.Provider.AvailableHashTypes)
        .ToHashSet();

    public IReadOnlySet<string> AllEnabledHashTypes => GetAvailableProviders(onlyEnabled: true)
        .SelectMany(p => p.EnabledHashTypes)
        .ToHashSet();

    public AbstractVideoHashingService(ISettingsProvider settingsProvider, IShokoEventHandler shokoEventHandler)
    {
        _settingsProvider = settingsProvider;
        _shokoEventHandler = shokoEventHandler;

        _shokoEventHandler.FileHashed += OnFileHashed;
    }

    ~AbstractVideoHashingService()
    {
        _shokoEventHandler.FileHashed -= OnFileHashed;
    }

    private void OnFileHashed(object? sender, FileEventArgs args) => FileHashed?.Invoke(this, args);

    public void AddProviders(IEnumerable<IHashProvider> providers)
    {
        if (_hashProviders is not null)
            return;

        _hashProviders = providers.ToDictionary(GetID);

        ProvidersUpdated?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<HashProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
    {
        if (_hashProviders is null)
            yield break;

        var order = _settings.Plugins.HashingProviders.Priority;
        var enabled = _settings.Plugins.HashingProviders.EnabledHashes;
        var orderedProviders = _hashProviders
            .OrderBy(p => order.IndexOf(p.Key) is -1)
            .ThenBy(p => order.IndexOf(p.Key))
            .ThenBy(p => p.Key)
            .Select((provider, index) => (provider, index));
        foreach (var ((id, provider), priority) in orderedProviders)
        {
            var enabledHashes = enabled.TryGetValue(id, out var h) ? h : id == _coreProviderID ? ["ED2K"] : [];
            if (onlyEnabled && enabledHashes.Count == 0)
                continue;

            yield return new()
            {
                ID = id,
                Provider = provider,
                EnabledHashTypes = enabledHashes,
                Priority = priority,
            };
        }
    }

    public HashProviderInfo GetProviderInfo(IHashProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (_hashProviders is null)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(provider))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public HashProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IHashProvider
    {
        if (_hashProviders is null)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public HashProviderInfo? GetProviderInfo(Guid providerID)
    {
        if (_hashProviders is null || !_hashProviders.TryGetValue(providerID, out var provider))
            return null;

        // We update the settings upon server start to ensure the priority and enabled states are accurate, so trust them.
        var priority = _settings.Plugins.HashingProviders.Priority.IndexOf(providerID);
        var enabled = _settings.Plugins.HashingProviders.EnabledHashes.TryGetValue(providerID, out var enabledHashes) ? enabledHashes : providerID == _coreProviderID ? ["ED2K"] : [];
        return new()
        {
            ID = providerID,
            Provider = provider,
            EnabledHashTypes = enabled,
            Priority = priority,
        };
    }

    public void UpdateProviders(params HashProviderInfo[] providers)
    {
        if (_hashProviders is null)
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
        var settings = _settingsProvider.GetSettings();
        var priority = existingProviders.Select(pI => pI.ID).ToList();
        if (!settings.Plugins.HashingProviders.Priority.SequenceEqual(priority))
        {
            settings.Plugins.HashingProviders.Priority = priority;
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
        if (!settings.Plugins.HashingProviders.EnabledHashes.SequenceEqual(enabled))
        {
            settings.Plugins.HashingProviders.EnabledHashes = enabled;
            changed = true;
        }

        if (changed)
        {
            _settingsProvider.SaveSettings(settings);
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<IReadOnlyList<IHashDigest>> GetHashesForFile(FileInfo fileInfo, IReadOnlyList<IHashDigest>? existingHashes = null, CancellationToken cancellationToken = default)
    {
        if (_hashProviders is null)
            return [];

        existingHashes ??= [];
        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        return ParallelMode ?
            await GetHashesForFileParallel(fileInfo, existingHashes, providers, cancellationToken) :
            await GetHashesForFileSequential(fileInfo, existingHashes, providers, cancellationToken);
    }

    private async Task<IReadOnlyList<IHashDigest>> GetHashesForFileSequential(FileInfo fileInfo, IReadOnlyList<IHashDigest> existingHashes, IReadOnlyList<HashProviderInfo> providers, CancellationToken cancellationToken)
    {
        var allHashes = new ConcurrentBag<IReadOnlyList<IHashDigest>>() { existingHashes };
        await Task.WhenAll(providers.Select(providerInfo => Task.Run(async () =>
        {
            var newHashes = await GetHashesForFileAndProvider(fileInfo, providerInfo, existingHashes, cancellationToken);
            allHashes.Add(newHashes);
        }, cancellationToken)));
        return allHashes
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

    /// <summary>
    /// Gets a unique ID for a release provider generated from its class name.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see cref="Guid" />.</returns>
    internal static Guid GetID(IHashProvider provider)
        => GetID(provider.GetType());

    /// <summary>
    /// Gets a unique ID for a release provider generated from its class name.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns><see cref="Guid" />.</returns>
    internal static Guid GetID(Type providerType)
        => new(MD5.HashData(Encoding.Unicode.GetBytes(providerType.FullName!)));
}
