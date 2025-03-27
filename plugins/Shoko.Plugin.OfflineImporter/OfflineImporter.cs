
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Exceptions;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Imports releases based on file names. By default operates in lax mode
/// which guesses based on any clues in all known files names for the video,
/// but can be configured to operate in strict mode, which means it will
/// only look for anidb bracket tags in the file and folder names to match
/// against.
/// </summary>
public partial class OfflineImporter(ILogger<OfflineImporter> logger, IApplicationPaths applicationPaths, IAniDBService anidbService, ConfigurationProvider<OfflineImporter.Configuration> configurationProvider) : IReleaseInfoProvider<OfflineImporter.Configuration>
{
    /// <inheritdoc/>
    public string Name => "Offline Importer";

    private const string OfflinePrefix = "offline://";

    /// <inheritdoc/>
    public string Description => """
        Imports releases based on file names. By default operates in lax mode
        which guesses based on any clues in all known files names for the video,
        but can be configured to operate in strict mode, which means it will
        only look for anidb bracket tags in the file and folder names to match
        against.
    """;

    /// <inheritdoc/>
    public Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
    {
        var folderRegex = StrictFolderNameCheckRegex();
        var filenameRegex = StrictFilenameCheckRegex();
        var config = configurationProvider.Load();
        foreach (var location in video.Locations)
        {
            if (!location.IsAvailable)
            {
                logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            var filePath = location.Path;
            var animeId = (int?)null;
            var releaseInfo = (ReleaseInfo?)null;
            var parts = location.RelativePath[1..].Split('/');
            var (folderName, fileName) = parts.Length > 1 ? (parts[^2], parts[^1]) : (null, parts[0]);
            var filenameMatch = filenameRegex.Match(fileName);
            var folderNameMatch = string.IsNullOrEmpty(folderName) ? null : folderRegex.Match(folderName);
            if (filenameMatch.Success)
            {
                logger.LogDebug("Found strict match: {FileName}", fileName);
                releaseInfo = (await GetReleaseInfoById(OfflinePrefix + filenameMatch.Value, cancellationToken))!;
                if (config.Mode is not Configuration.MatchMode.StrictAndFast && await GetReleaseInfoByFileName(filePath, cancellationToken) is { } extraReleaseInfo)
                {
                    releaseInfo.ReleaseURI = extraReleaseInfo.ReleaseURI;
                    releaseInfo.Group = extraReleaseInfo.Group;
                    releaseInfo.Revision = extraReleaseInfo.Revision;
                    releaseInfo.IsCensored = extraReleaseInfo.IsCensored;
                    releaseInfo.IsCreditless = extraReleaseInfo.IsCreditless;
                    releaseInfo.IsChaptered = extraReleaseInfo.IsChaptered;
                    releaseInfo.IsCorrupted = extraReleaseInfo.IsCorrupted;
                    releaseInfo.Source = extraReleaseInfo.Source;
                    releaseInfo.MediaInfo = extraReleaseInfo.MediaInfo;
                    releaseInfo.CrossReferences = extraReleaseInfo.CrossReferences;
                    releaseInfo.Metadata = extraReleaseInfo.Metadata;
                    releaseInfo.ReleasedAt = extraReleaseInfo.ReleasedAt;
                    releaseInfo.CreatedAt = extraReleaseInfo.CreatedAt;
                }
            }
            else if (config.Mode is Configuration.MatchMode.Lax)
            {
                releaseInfo = await GetReleaseInfoByFileName(filePath, cancellationToken);
            }

            if (releaseInfo is not { CrossReferences.Count: > 0 })
                continue;

            if (video.MediaInfo is { } mediaInfo)
            {
                if (mediaInfo.Encoded is { } encodedAt && (releaseInfo.ReleasedAt is null || releaseInfo.ReleasedAt > DateOnly.FromDateTime(encodedAt)))
                    releaseInfo.ReleasedAt = DateOnly.FromDateTime(encodedAt);

                releaseInfo.IsChaptered = mediaInfo.Chapters.Count > 0;
            }
            releaseInfo.FileSize = video.Size;
            releaseInfo.Hashes = video.Hashes.Hashes
                .Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata })
                .ToList();

            if (releaseInfo.CrossReferences.Any(xref => xref.AnidbAnimeID is null or <= 0))
            {
                if (folderNameMatch is { Success: true })
                    animeId = int.Parse(folderNameMatch.Value[6..^1].Trim());
                else if (config.Mode is Configuration.MatchMode.Lax && !string.IsNullOrWhiteSpace(folderName))
                    animeId = GetAnimeIdByFolderName(folderName, cancellationToken);
                if (animeId is > 0)
                    foreach (var xref in releaseInfo.CrossReferences)
                        if (xref.AnidbAnimeID is null or <= 0)
                            xref.AnidbAnimeID = animeId;
            }

            return releaseInfo;
        }

        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoByFileName(string filePath, CancellationToken cancellationToken)
    {
        if (MatchRuleResult.Match(filePath) is not { Success: true } match)
            return null;

        logger.LogDebug("Found potential match {ShowName} for {FileName}", match.ShowName, filePath);
        if (anidbService.Search(match.ShowName!) is not { Count: > 0 } searchResults)
        {
            logger.LogDebug("No search results found for {ShowName}", match.ShowName);
            return null;
        }

        logger.LogDebug("Found {Count} search results for {ShowName0}, picking first one; {ShowName1}", searchResults.Count, match.ShowName, searchResults[0].DefaultTitle);
        if (searchResults[0].AnidbAnime is not { } anime)
        {
            logger.LogDebug("Refreshing AniDB Anime {AnimeName} (Anime={AnimeID})", searchResults[0].DefaultTitle, searchResults[0].ID);
            try
            {
                anime = await anidbService.RefreshByID(searchResults[0].ID, AnidbRefreshMethod.Cache | AnidbRefreshMethod.SkipTmdbUpdate).ConfigureAwait(false);
                if (anime is null)
                {
                    anime = await anidbService.RefreshByID(searchResults[0].ID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.SkipTmdbUpdate | AnidbRefreshMethod.IgnoreTimeCheck).ConfigureAwait(false);
                    if (anime is null)
                        return null;
                }
            }
            catch (AnidbHttpBannedException ex)
            {
                logger.LogWarning("Got banned while searching for {ShowName}: {Message}", match.ShowName, ex.Message);
                return null;
            }
        }

        var episodes = anime.Episodes
            .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
            .ToList();
        if (episodes.Count == 0)
            return null;

        var group = (ReleaseGroup?)null;
        if (!string.IsNullOrEmpty(match.ReleaseGroup))
        {
            if (configurationProvider.Load().MapAsAnidbGroupIfPossible)
                group = OfflineReleaseGroupSearch.LookupByName(match.ReleaseGroup, applicationPaths);
            group ??= new() { ID = match.ReleaseGroup, Name = match.ReleaseGroup, ShortName = match.ReleaseGroup, Source = "Offline" };
        }

        var isCreditless = CreditlessRegex().IsMatch(filePath);
        var isCensored = CensoredRegex().Match(filePath) is { Success: true } censoredResult ? !censoredResult.Groups["isDe"].Success : (bool?)null;
        return new ReleaseInfo()
        {
            ID = OfflinePrefix + episodes.Select(x => $"{anime.ID}-{x.ID}").Join(','),
            Group = group,
            OriginalFilename = Path.GetFileName(filePath),
            Revision = match.Version ?? 1,
            ProviderName = "Offline",
            CrossReferences = episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = x.ID }).ToList(),
            Metadata = JsonConvert.SerializeObject(match),
            IsCensored = isCensored,
            IsCreditless = isCreditless,
            // Assume the creation date has been properly set in the file-system.
            ReleasedAt = DateOnly.FromDateTime(File.GetCreationTimeUtc(filePath)),
        };
    }

    private int? GetAnimeIdByFolderName(string folderName, CancellationToken cancellationToken)
    {
        // TODO: Implement this logic if needed.
        return null;
    }

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
    {
        if (!releaseId.StartsWith(OfflinePrefix))
        {
            logger.LogWarning("Invalid release id: {ReleaseId}", releaseId);
            return Task.FromResult<ReleaseInfo?>(null);
        }

        releaseId = releaseId[OfflinePrefix.Length..];
        var segmentRegex = SegmentRegex();
        var segments = releaseId
            .Split(',')
            .Select(part => segmentRegex.Match(part))
            .ToList();
        if (segments.Count is 0 || segments.Any(part => part is null || !part.Success))
        {
            logger.LogWarning("Malformed release id: {ReleaseId}", releaseId);
            return Task.FromResult<ReleaseInfo?>(null);
        }
        var previousPercentage = 0;
        var crossReferences = new List<ReleaseVideoCrossReference>();
        foreach (var match in segments)
        {
            var animeId = match.Groups["animeId"].Success ? int.Parse(match.Groups["animeId"].Value) : (int?)null;
            var episodeId = int.Parse(match.Groups["episodeId"].Value);
            var percentageStart = 100;
            var percentageEnd = -1;
            if (match.Groups["percentRangeStartOrWholeRange"].Success)
            {
                percentageStart = int.Parse(match.Groups["percentRangeStartOrWholeRange"].Value);
                if (match.Groups["percentRangeEnd"].Success)
                    percentageEnd = int.Parse(match.Groups["percentRangeEnd"].Value);
            }
            if (percentageEnd is -1)
            {
                percentageEnd = percentageStart + previousPercentage;
                percentageStart = previousPercentage;
            }
            if (percentageEnd > 100)
            {
                percentageEnd = 100;
                percentageStart = 0;
            }
            previousPercentage = percentageEnd;
            if (previousPercentage >= 100)
                previousPercentage = 0;
            crossReferences.Add(new ReleaseVideoCrossReference
            {
                AnidbAnimeID = animeId,
                AnidbEpisodeID = episodeId,
                PercentageStart = percentageStart,
                PercentageEnd = percentageEnd,
            });
        }

        return Task.FromResult<ReleaseInfo?>(new() { CrossReferences = crossReferences, });
    }

    [GeneratedRegex(@"\[anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\]|,),?)+\]|\(anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\)|,),?)+\)|\{anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFolderNameCheckRegex();

    [GeneratedRegex(@"\[anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\]|,),?)+\]|\(anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\)|,),?)+\)|\{anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFilenameCheckRegex();

    [GeneratedRegex(@"(?<=^|,)\s*(?:(?<animeId>\d+)[=\- \.])?(?<episodeId>\d+)(?:'(?<percentRangeStartOrWholeRange>\d+)(?:\-(?<percentRangeEnd>\d+))?)?\s*(?=$|,),?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();

    [GeneratedRegex(@"(?:(?<![a-z0-9])(?:nc|credit[\- ]?less)[\s_.]*(?:ed|op)(?![a-z]))(?:[\s_.]*(?:\d+(?!\d*p)))?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreditlessRegex();

    [GeneratedRegex(@"\b((?<isDe>de)?cen(?:[sz]ored)?)\b", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CensoredRegex();

    /// <summary>
    /// Configure how the offline importer works.
    /// </summary>
    public class Configuration : IReleaseInfoProviderConfiguration, INewtonsoftJsonConfiguration
    {
        /// <summary>
        /// Match mode to use during the import.
        /// </summary>
        [DefaultValue(MatchMode.Lax)]
        public MatchMode Mode { get; set; } = MatchMode.Lax;

        /// <summary>
        /// If set to true, hashes stored in the database will be included in
        /// the provided release info.
        /// </summary>
        [Display(Name = "Store existing hashes")]
        [DefaultValue(true)]
        public bool StoreHashes { get; set; } = true;

        /// <summary>
        /// If enabled, the importer will attempt to map the release group to a
        /// known AniDB release group if possible.
        /// </summary>
        public bool MapAsAnidbGroupIfPossible { get; set; }

        /// <summary>
        /// Match mode to use during the import.
        /// </summary>
        public enum MatchMode
        {
            /// <summary>
            /// Lax mode.
            /// </summary>
            [Display(Name = "Lax Mode")]
            [EnumMember(Value = "lax")]
            Lax = 0,

            /// <summary>
            /// Strict mode, with extra info extracted from the file name.
            /// </summary>
            [Display(Name = "Strict Mode (Include Extra Info)")]
            [EnumMember(Value = "strict")]
            StrictAndInfo = 1,

            /// <summary>
            /// Strict mode, with no extra info.
            /// </summary>
            [Display(Name = "Strict Mode (No Extra Info)")]
            [EnumMember(Value = "strict-fast")]
            StrictAndFast = 2,
        }
    }
}
