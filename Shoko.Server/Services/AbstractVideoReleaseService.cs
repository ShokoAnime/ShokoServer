
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractVideoReleaseService(ISettingsProvider settingsProvider, DatabaseReleaseInfoRepository releaseInfoRepository) : IVideoReleaseService
{
    private Dictionary<string, IReleaseInfoProvider>? _releaseInfoProviders = null;

    public event EventHandler<VideoReleaseEventArgs>? VideoReleaseSaved;

    public event EventHandler<VideoReleaseEventArgs>? VideoReleaseDeleted;

    public void AddProviders(IEnumerable<IReleaseInfoProvider> providers)
    {
        if (_releaseInfoProviders is not null)
            return;

        _releaseInfoProviders = providers
            .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders()
    {
        if (_releaseInfoProviders is null)
            yield break;

        var settings = settingsProvider.GetSettings().Plugins.ReleaseProvider;
        var order = settings.Priority;
        var enabled = settings.Enabled;
        var orderedProviders = _releaseInfoProviders.Values
            .OrderBy(p => order.IndexOf(p.Name) is -1)
            .ThenBy(p => order.IndexOf(p.Name))
            .ThenBy(p => p.Name)
            .Select((provider, index) => (provider, index));
        foreach (var (provider, index) in orderedProviders)
        {
            yield return new()
            {
                Provider = provider,
                Enabled = enabled.TryGetValue(provider.Name, out var isEnabled) && isEnabled,
                Priority = index,
            };
        }
    }

    public IReleaseInfoProvider? GetProviderByID(string providerID)
        => _releaseInfoProviders?.TryGetValue(providerID, out var provider) ?? false ? provider : null;

    public void UpdateProviders(params ReleaseInfoProviderInfo[] providers)
    {
        if (_releaseInfoProviders is null)
            return;

        var existingProviders = GetAvailableProviders().ToList();
        foreach (var providerInfo in providers)
        {
            var wantedIndex = providerInfo.Priority;
            var existingIndex = existingProviders.FindIndex(p => p.Provider == providerInfo.Provider);
            if (existingIndex is -1)
                continue;

            // Enable or disable provider.
            if (providerInfo.Enabled != existingProviders[existingIndex].Enabled)
                existingProviders[existingIndex].Enabled = providerInfo.Enabled;

            // Move index.
            if (wantedIndex != existingIndex)
            {
                var pI = existingProviders[existingIndex];
                existingProviders.RemoveAt(existingIndex);
                if (wantedIndex < 0)
                    existingProviders.Add(pI);
                else if (wantedIndex > existingIndex && wantedIndex > 0)
                    existingProviders.Insert(wantedIndex - 1, pI);
                else
                    existingProviders.Insert(wantedIndex, pI);
            }
        }

        var settings = settingsProvider.GetSettings();
        settings.Plugins.ReleaseProvider.Priority = existingProviders
            .Select(pI => pI.Provider.Name)
            .ToList();
        settings.Plugins.ReleaseProvider.Enabled = existingProviders
            .ToDictionary(p => p.Provider.Name, p => p.Enabled);
        settingsProvider.SaveSettings(settings);
    }

    public IReleaseInfo? GetCurrentReleaseForVideo(IVideo video)
        => releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);

    public async Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, CancellationToken cancellationToken = default)
    {
        IReleaseInfo? releaseInfo = null;
        foreach (var providerInfo in GetAvailableProviders())
        {
            if (!providerInfo.Enabled)
                continue;

            var provider = providerInfo.Provider;
            var release = await provider.GetReleaseInfoForVideo(video, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (release is null)
                continue;

            releaseInfo = new ReleaseInfoWithProvider(release, provider.Name);
            break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!saveRelease || releaseInfo is null)
            return releaseInfo;

        return await SaveReleaseForVideo(video, releaseInfo);
    }

    public Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User")
        => SaveReleaseForVideo(video, new ReleaseInfoWithProvider(release, providerName));

    public async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release)
    {
        var existingRelease = releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);
        if (existingRelease is not null)
            await ClearReleaseForVideo(video, existingRelease);

        var databaseReleaseInfo = new DatabaseReleaseInfo(video, release);
        if (existingRelease is not null)
            databaseReleaseInfo.DatabaseReleaseInfoID = existingRelease.DatabaseReleaseInfoID;

        releaseInfoRepository.Save(databaseReleaseInfo);

        VideoReleaseSaved?.Invoke(null, new(video, databaseReleaseInfo));

        return databaseReleaseInfo;
    }

    public async Task<bool> ClearReleaseForVideo(IVideo video)
    {
        var existingRelease = releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);
        if (existingRelease is null)
            return true;

        return await ClearReleaseForVideo(video, existingRelease);
    }

    private Task<bool> ClearReleaseForVideo(IVideo video, DatabaseReleaseInfo releaseInfo)
    {
        releaseInfoRepository.Delete(releaseInfo);

        VideoReleaseDeleted?.Invoke(null, new(video, releaseInfo));

        return Task.FromResult(true);
    }
}
