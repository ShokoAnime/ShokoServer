
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
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Anidb;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Exceptions;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Imports releases based on file paths.
///
/// By default operates only using the local database, local cache, and
/// local title search. Online refresh can be enabled if desired, but it's
/// designed to function without it.
///
/// You have three different operation modes; lax, strict (with extra info),
/// and strict (without extra info). By default it operates in lax mode,
/// which guesses based on all clues if finds in the file name. Then we have
/// strict mode, where it will only look for anidb bracket tags in the file
/// and folder names to match against, optionally with extra info from the
/// lax mode enabled without using the lax search for the cross-reference
/// info.
/// </summary>
public partial class OfflineImporter(ILogger<OfflineImporter> logger, IApplicationPaths applicationPaths, IAniDBService anidbService, ConfigurationProvider<OfflineImporter.Configuration> configurationProvider) : IReleaseInfoProvider<OfflineImporter.Configuration>
{
    /// <inheritdoc/>
    public string Name => "Offline Importer";

    private const string OfflinePrefix = "offline://";

    /// <inheritdoc/>
    public string Description => """
        Imports releases based on file paths.

        By default operates only using the local database, local cache, and
        local title search. Online refresh can be enabled if desired, but it's
        designed to function without it.

        You have three different operation modes; lax, strict (with extra info),
        and strict (without extra info). By default it operates in lax mode,
        which guesses based on all clues if finds in the file name. Then we have
        strict mode, where it will only look for anidb bracket tags in the file
        and folder names to match against, optionally with extra info from the
        lax mode enabled without using the lax search for the cross-reference
        info.
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
            var filePath = location.Path;
            if (string.IsNullOrEmpty(filePath))
            {
                logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            if (!config.SkipAvailabilityCheck && !location.IsAvailable)
            {
                logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            var releaseInfo = (ReleaseInfo?)null;
            var parts = location.RelativePath[1..].Split('/');
            var (folderName, fileName) = parts.Length > 1 ? (parts[^2], parts[^1]) : (null, parts[0]);
            var filenameMatch = filenameRegex.Match(fileName);
            var match = config.Mode is not Configuration.MatchMode.StrictAndFast
                ? MatchRuleResult.Match(filePath)
                : MatchRuleResult.Empty;
            if (filenameMatch.Success)
                releaseInfo = (await GetReleaseInfoById(OfflinePrefix + filenameMatch.Value, cancellationToken))!;
            else if (config.Mode is Configuration.MatchMode.Lax && match is { Success: true })
                releaseInfo = await GetReleaseInfoByFileName(match, cancellationToken);
            if (releaseInfo is not { CrossReferences.Count: > 0 })
                continue;

            if (releaseInfo.CrossReferences.Any(xref => xref.AnidbAnimeID is null or <= 0))
            {
                var animeId = (int?)null;
                var folderNameMatch = string.IsNullOrEmpty(folderName) ? null : folderRegex.Match(folderName);
                if (folderNameMatch is { Success: true })
                    animeId = int.Parse(folderNameMatch.Value[6..^1].Trim());
                if (animeId is > 0)
                    foreach (var xref in releaseInfo.CrossReferences)
                        if (xref.AnidbAnimeID is null or <= 0)
                            xref.AnidbAnimeID = animeId;
            }

            releaseInfo.FileSize = video.Size;
            releaseInfo.Hashes = video.Hashes.Hashes
                .Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata })
                .ToList();

            if (video.MediaInfo is { } mediaInfo)
            {
                if (mediaInfo.Encoded is { } encodedAt && encodedAt > DateTime.MinValue)
                    releaseInfo.ReleasedAt = DateOnly.FromDateTime(encodedAt);

                releaseInfo.IsChaptered = mediaInfo.Chapters.Count > 0;
            }

            if (match is { Success: true })
            {
                var group = (ReleaseGroup?)null;
                if (!string.IsNullOrEmpty(match.ReleaseGroup))
                {
                    if (configurationProvider.Load().MapAsAnidbGroupIfPossible)
                        group = OfflineReleaseGroupSearch.LookupByName(match.ReleaseGroup, applicationPaths);
                    group ??= new() { ID = match.ReleaseGroup, Name = match.ReleaseGroup, ShortName = match.ReleaseGroup, Source = "Offline" };
                }

                var isCreditless = CreditlessRegex().IsMatch(match.FilePath);
                var isCensored = CensoredRegex().Match(match.FilePath) is { Success: true } censoredResult ? !censoredResult.Groups["isDe"].Success : (bool?)null;

                releaseInfo.Group = group;
                releaseInfo.OriginalFilename = Path.GetFileName(match.FilePath);
                releaseInfo.Revision = match.Version ?? 1;
                releaseInfo.Metadata = JsonConvert.SerializeObject(match);
                releaseInfo.IsCensored = isCensored;
                releaseInfo.IsCreditless = isCreditless;
                // Assume the creation date has been properly set in the file-system.
                releaseInfo.ReleasedAt ??= DateOnly.FromDateTime(File.GetCreationTimeUtc(match.FilePath));
            }

            return releaseInfo;
        }

        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoByFileName(MatchRuleResult match, CancellationToken cancellationToken)
    {
        logger.LogDebug("Found potential match {ShowName} for {FileName}", match.ShowName, match.FilePath);
        if (anidbService.Search(match.ShowName!) is not { Count: > 0 } searchResults)
        {
            logger.LogDebug("No search results found for {ShowName}", match.ShowName);
            return null;
        }

        logger.LogDebug("Found {Count} search results for {ShowName0}, picking first one; {ShowName1}", searchResults.Count, match.ShowName, searchResults[0].DefaultTitle);
        return await GetReleaseInfoForMatchAndAnime(match, searchResults[0], cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatchAndAnime(MatchRuleResult match, IAnidbAnimeSearchResult searchResult, CancellationToken cancellationToken, int depth = 0)
    {
        var anime = searchResult.AnidbAnime;
        if (
            anime is null ||
            !anime.Episodes.Any(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
        )
        {
            logger.LogDebug("Refreshing AniDB Anime {AnimeName} (Anime={AnimeID})", searchResult.DefaultTitle, searchResult.ID);
            anime ??= await anidbService.RefreshByID(searchResult.ID, AnidbRefreshMethod.Cache | AnidbRefreshMethod.SkipTmdbUpdate, cancellationToken).ConfigureAwait(false);
            if (configurationProvider.Load().AllowRemote && (
                anime is null ||
                !anime.Episodes.Any(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
            ))
            {
                try
                {
                    anime = await anidbService.RefreshByID(searchResult.ID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.SkipTmdbUpdate, cancellationToken).ConfigureAwait(false);
                    if (anime is null)
                        return null;
                }
                catch (AnidbHttpBannedException ex)
                {
                    logger.LogWarning("Got banned while refreshing {AnimeName}: {Message}", searchResult.DefaultTitle, ex.Message);
                    return null;
                }
            }

            if (anime is null || !anime.Episodes.Any(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart))
                return null;
        }

        // Prevents following movies when searching for sequels, since we can't
        // know beforehand if the anidb anime is a movie before we potentially
        // fetch it.
        if (depth is > 0 && anime is { Type: AnimeType.Movie or AnimeType.Unknown })
        {
            logger.LogDebug("Skipping unknown or movie {ShowName}", anime.DefaultTitle);
            return null;
        }

        var allEpisodes = anime.Episodes.ToList();
        var episodes = allEpisodes
            .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
            .ToList();
        if (episodes.Count == 0)
        {
            var highestEpisodeNumber = allEpisodes.Count > 0 ? allEpisodes.Max(x => x.EpisodeNumber) : 0;
            if (match.EpisodeStart > highestEpisodeNumber)
            {
                logger.LogDebug("Found episode range is above last episode, trying to find sequel for {ShowName}", match.ShowName);
                var sequels = anime.RelatedSeries.Where(x => x.RelationType == RelationType.Sequel)
                    .ToList();
                if (sequels.Count == 0)
                {
                    logger.LogDebug("No sequels found for {ShowName}", match.ShowName);
                    return null;
                }

                foreach (var sequel in sequels)
                {
                    var sequelSearch = anidbService.SearchByID(sequel.RelatedID);
                    if (sequelSearch is null)
                    {
                        logger.LogDebug("Unknown sequel by ID {RelatedID} for {ShowName}", searchResult.ID, anime.DefaultTitle);
                        continue;
                    }

                    var sequelMatch = new MatchRuleResult()
                    {
                        Success = true,
                        FilePath = match.FilePath,
                        FileExtension = match.FileExtension,
                        EpisodeName = match.EpisodeName,
                        EpisodeEnd = match.EpisodeEnd - highestEpisodeNumber,
                        EpisodeStart = match.EpisodeStart - highestEpisodeNumber,
                        EpisodeType = match.EpisodeType,
                        ReleaseGroup = match.ReleaseGroup,
                        Season = match.Season,
                        ShowName = match.ShowName,
                        Version = match.Version,
                        RuleName = match.RuleName,
                    };
                    logger.LogDebug("Attempting sequel {ShowName} (Anime={AnimeID})", sequelSearch.DefaultTitle, sequel.RelatedID);
                    var sequelResult = await GetReleaseInfoForMatchAndAnime(sequelMatch, sequelSearch, cancellationToken, depth + 1).ConfigureAwait(false);
                    if (sequelResult is not null)
                    {
                        logger.LogDebug("Found sequel for {ShowName}", match.ShowName);
                        return sequelResult;
                    }
                }

                logger.LogDebug("No matched sequels found for {ShowName}", match.ShowName);
                return null;
            }

            logger.LogDebug("No episodes found for {ShowName}", match.ShowName);
            return null;
        }

        var releaseInfo = new ReleaseInfo()
        {
            ID = OfflinePrefix + episodes.Select(x => $"{anime.ID}-{x.ID}").Join(','),
            CrossReferences = episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = x.ID }).ToList(),
        };
        return releaseInfo;
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

        return Task.FromResult<ReleaseInfo?>(new() { ID = releaseId, CrossReferences = crossReferences, });
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
        [Display(Name = "Map release group to AniDB")]
        [DefaultValue(false)]
        public bool MapAsAnidbGroupIfPossible { get; set; } = false;

        /// <summary>
        /// If disabled, the importer will not attempt to refresh AniDB data
        /// remotely if not found locally in the database or in the cache.
        /// </summary>
        [Display(Name = "Enable remote refresh")]
        [DefaultValue(false)]
        public bool AllowRemote { get; set; } = false;

        /// <summary>
        /// If enabled, the importer will skip the availability check and
        /// check all video file paths regardless of availability.
        /// </summary>
        [Badge("Debug", Theme = DisplayColorTheme.Warning)]
        [Visibility(Advanced = true)]
        public bool SkipAvailabilityCheck { get; set; }

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
