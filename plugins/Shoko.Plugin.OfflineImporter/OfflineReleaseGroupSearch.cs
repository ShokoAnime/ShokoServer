
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Release;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Responsible for handling the offline release group search.
/// </summary>
public static class OfflineReleaseGroupSearch
{
    private const string GroupUrl = "aHR0cHM6Ly90ZXN0LmFuaS56aXAvZ3JvdXA=";

    private static readonly ReaderWriterLockSlim _accessLock = new(LockRecursionPolicy.SupportsRecursion);

    private static ILookup<string, AniReleaseGroup>? _groupsByName;

    private static IReadOnlyDictionary<int, AniReleaseGroup>? _groupsByID;

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

    /// <summary>
    /// Initialize the groups before use.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    public static void InitializeBeforeUse(IApplicationPaths applicationPaths)
    {
        if (_groupsByName is null || _groupsByID is null)
            EnsureGroupsExists(applicationPaths);
    }

    private static void EnsureGroupsExists(IApplicationPaths? applicationPaths)
    {
        if (applicationPaths is null || (_groupsByName is not null && _groupsByID is not null && _nextUpdate is not null && _nextUpdate.Value > DateTime.Now))
            return;

        try
        {
            _accessLock.EnterWriteLock();
            if (applicationPaths is null || (_groupsByName is not null && _groupsByID is not null && _nextUpdate is not null && _nextUpdate.Value > DateTime.Now))
                return;

            var filePath = Path.Combine(applicationPaths.DataPath, "groups.json");
            try
            {
                if (!File.Exists(filePath))
                    DownloadJson(applicationPaths);
            }
            catch
            {
                // ignored
            }

            if (!File.Exists(filePath))
                return;

            var lastWriteTime = File.GetLastWriteTime(filePath);
            try
            {
                if (DateTime.Now - lastWriteTime > TimeSpan.FromHours(24))
                    DownloadJson(applicationPaths);
            }
            catch
            {
                // ignored
            }

            var json = File.ReadAllText(filePath);

            var groups = JsonConvert.DeserializeObject<List<AniReleaseGroup>>(json) ?? [];
            _groupsByName = groups
                .SelectMany<AniReleaseGroup, (string Key, AniReleaseGroup Value)>(group =>
                {
                    if (group.ShortName != group.Name)
                        return [(Key: group.Name, Value: group), (Key: group.ShortName, Value: group)];

                    return [(Key: group.Name, Value: group)];
                })
                .OrderBy(tuple => tuple.Key)
                .ThenBy(tuple => tuple.Value.CreatedAt)
                .ToLookup(tuple => tuple.Key, tuple => tuple.Value);
            _groupsByID = groups
                .OrderBy(group => group.Name)
                .ThenBy(group => group.CreatedAt)
                .DistinctBy(group => group.ID)
                .ToDictionary(group => group.ID);

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
        var response = httpClient.GetAsync(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(GroupUrl))).GetAwaiter().GetResult();
        var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        if (response.Content.Headers.ContentEncoding.FirstOrDefault()?.ToLowerInvariant() is "gzip")
        {
            stream = new GZipStream(stream, CompressionMode.Decompress);
        }
        var path = Path.Combine(applicationPaths.DataPath, "groups.json");
        stream.CopyTo(File.Create(path));
    }

    /// <summary>
    /// Looks up a release group by name in the offline data.
    /// </summary>
    /// <param name="name">The name of the release group to look up.</param>
    /// <param name="applicationPaths">The application paths to use for storing the offline data.</param>
    /// <returns>The release group if found, or null otherwise.</returns>
    public static ReleaseGroup? LookupByName(string name, IApplicationPaths? applicationPaths = null)
    {
        EnsureGroupsExists(applicationPaths);

        try
        {
            _accessLock.EnterReadLock();
            if (_groupsByName is null)
                return null;

            if (_groupsByName.Contains(name) && _groupsByName[name].FirstOrDefault() is { } group)
                return new() { ID = group.ID.ToString(), Name = group.Name, ShortName = group.ShortName, Source = "AniDB" };
        }
        finally
        {
            _accessLock.ExitReadLock();
        }

        return null;
    }

    /// <summary>
    /// Looks up a release group by ID in the offline data.
    /// </summary>
    /// <param name="groupID">The ID of the release group to look up.</param>
    /// <param name="applicationPaths">The application paths to use for storing the offline data.</param>
    /// <returns>The release group if found, or null otherwise.</returns>
    public static ReleaseGroup? LookupByID(int groupID, IApplicationPaths? applicationPaths = null)
    {
        if (groupID is not > 0)
            return null;

        EnsureGroupsExists(applicationPaths);

        try
        {
            _accessLock.EnterReadLock();
            if (_groupsByID is null)
                return null;

            if (_groupsByID.TryGetValue(groupID, out var group))
                return new() { ID = group.ID.ToString(), Name = group.Name, ShortName = group.ShortName, Source = "AniDB" };
        }
        finally
        {
            _accessLock.ExitReadLock();
        }

        return null;
    }
}
