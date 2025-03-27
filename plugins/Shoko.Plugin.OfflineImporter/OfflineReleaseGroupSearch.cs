
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Responsible for handling the offline release group search.
/// </summary>
public static class OfflineReleaseGroupSearch
{
    private const string GroupUrl = "https://test.ani.zip/group";

    private static readonly ReaderWriterLockSlim _accessLock = new(LockRecursionPolicy.SupportsRecursion);

    private static ILookup<string, AniReleaseGroup>? _groups;

    private static DateTime? _nextUpdate;

    private class AniReleaseGroup
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("tag")]
        public string ShortName { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public int CreatedAt { get; set; }
    }

    private static void EnsureGroupsExists(IApplicationPaths applicationPaths)
    {
        try
        {
            _accessLock.EnterWriteLock();
            if (_groups is not null && _nextUpdate is not null && _nextUpdate.Value > DateTime.Now)
                return;

            var filePath = Path.Combine(applicationPaths.ProgramDataPath, "groups.json");
            if (!File.Exists(filePath))
                DownloadJson(applicationPaths);

            if (!File.Exists(filePath))
                return;

            var lastWriteTime = File.GetLastWriteTime(filePath);
            if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
                DownloadJson(applicationPaths);

            var json = File.ReadAllText(filePath);

            var groups = JsonConvert.DeserializeObject<List<AniReleaseGroup>>(json) ?? [];
            _groups = groups
                .SelectMany<AniReleaseGroup, (string Key, AniReleaseGroup Value)>(group =>
                {
                    if (group.ShortName != group.Name)
                        return [(Key: group.Name, Value: group), (Key: group.ShortName, Value: group)];

                    return [(Key: group.Name, Value: group)];
                })
                .OrderBy(tuple => tuple.Key)
                .ThenBy(tuple => tuple.Value.CreatedAt)
                .ToLookup(tuple => tuple.Key, tuple => tuple.Value);

            // Set the next update to run in 24 hours, unless we somehow failed
            // to download it in the last 24 hours, then set it to 4 hours.
            _nextUpdate = lastWriteTime.AddHours(24);
            if (_nextUpdate.Value < DateTime.Now)
                _nextUpdate = DateTime.Now.AddHours(4);
        }
        finally
        {
            _accessLock.ExitWriteLock();
        }

    }

    private static void DownloadJson(IApplicationPaths applicationPaths)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Shoko.Plugin.OfflineImporter/1.0");
        var json = httpClient.GetByteArrayAsync(GroupUrl).GetAwaiter().GetResult();
        File.WriteAllBytes(Path.Combine(applicationPaths.ProgramDataPath, "groups.json"), json);
    }

    /// <summary>
    /// Looks up a release group by name in the offline data.
    /// </summary>
    /// <param name="name">The name of the release group to look up.</param>
    /// <param name="applicationPaths">The application paths to use for storing the offline data.</param>
    /// <returns>The release group if found, or null otherwise.</returns>
    public static ReleaseGroup? LookupByName(string name, IApplicationPaths applicationPaths)
    {
        EnsureGroupsExists(applicationPaths);

        try
        {
            _accessLock.EnterReadLock();
            if (_groups is null)
                return null;

            if (_groups.Contains(name) && _groups[name].FirstOrDefault() is { } group)
                return new() { ID = group.ID.ToString(), Name = group.Name, ShortName = group.ShortName, Source = "AniDB" };
        }
        finally
        {
            _accessLock.ExitReadLock();
        }

        return null;
    }
}
