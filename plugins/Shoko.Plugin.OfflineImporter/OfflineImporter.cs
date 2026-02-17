
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Components;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Hashing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Release;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;

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
public partial class OfflineImporter : IReleaseInfoProvider<OfflineImporter.Configuration>
{
    private readonly ILogger<OfflineImporter> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IAnidbService _anidbService;

    private readonly IMetadataService _metadataService;

    private readonly ConfigurationProvider<Configuration> _configurationProvider;

    private IReadOnlyList<ParsedFileResult.CompiledRule> _rules;

    /// <inheritdoc/>
    public string Name => "Offline Importer";

    private const string FilePrefix = "file://";

    private const string IdPrefix = "offline://";

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

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineImporter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="anidbService">The anidb service.</param>
    /// <param name="metadataService">The metadata service.</param>
    /// <param name="configurationProvider">The configuration provider.</param>
    public OfflineImporter(
        ILogger<OfflineImporter> logger,
        IApplicationPaths applicationPaths,
        IAnidbService anidbService,
        IMetadataService metadataService,
        ConfigurationProvider<Configuration> configurationProvider
    )
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
        _anidbService = anidbService;
        _metadataService = metadataService;
        _configurationProvider = configurationProvider;
        _rules = configurationProvider.Load().Rules.Select(x => x.ToMatchRule()).ToList();

        _configurationProvider.Saved += OnConfigurationChanged;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="OfflineImporter"/> class.
    /// </summary>
    ~OfflineImporter()
    {
        _configurationProvider.Saved -= OnConfigurationChanged;
    }

    private void OnConfigurationChanged(object? sender, ConfigurationSavedEventArgs<Configuration> e)
    {
        _rules = e.Configuration.Rules.Select(x => x.ToMatchRule()).ToList();
    }

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken)
    {
        var (video, isAutomatic) = context;
        var config = _configurationProvider.Load();
        if (config.AutoMatch is Configuration.AutoMatchMode.Disabled)
        {
            _logger.LogDebug("Auto matching is disabled.");
            return null;
        }
        if (config.AutoMatch is Configuration.AutoMatchMode.RulesOnly && !config.AutoMatchRules.Any())
        {
            _logger.LogDebug("No rules configured for auto matching.");
            return null;
        }

        _logger.LogDebug("Getting release info for {Video}", video.ID);
        var videoFiles = video.Files;
        foreach (var location in videoFiles)
        {
            var filePath = location.Path;
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            if (!_configurationProvider.Load().SkipAvailabilityCheck && !location.IsAvailable)
            {
                _logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            var releaseInfo = await GetReleaseInfoByFilePath(filePath, location.RelativePath, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is null)
                continue;

            if (isAutomatic && config.AutoMatch is Configuration.AutoMatchMode.RulesOnly && !CheckAutomaticRelease(filePath, videoFiles, releaseInfo, config))
            {
                _logger.LogDebug("Release info {ReleaseInfo} is was not allowed by any of the {Count} rules", releaseInfo, config.AutoMatchRules.Count);
                return null;
            }

            releaseInfo.FileSize = video.Size;
            releaseInfo.Hashes = video.Hashes
                .Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata })
                .ToList();

            if (video.MediaInfo is { } mediaInfo)
            {
                if (mediaInfo.Encoded is { } encodedAt && encodedAt > DateTime.MinValue && encodedAt != DateTime.UnixEpoch)
                    releaseInfo.ReleasedAt = DateOnly.FromDateTime(encodedAt);

                releaseInfo.IsChaptered = mediaInfo.Chapters.Count > 0;
            }

            return releaseInfo;
        }

        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoByFilePath(string filePath, string relativePath, CancellationToken cancellationToken)
    {
        var config = _configurationProvider.Load();
        var animeId = (int?)null;
        var releaseInfo = (ReleaseInfo?)null;
        var parts = relativePath[1..].Split('/');
        var (folderName, fileName) = parts.Length > 1 ? (parts[^2], parts[^1]) : (null, parts[0]);
        var filenameMatch = StrictFilenameCheckRegex().Match(fileName);
        var folderNameMatch = string.IsNullOrEmpty(folderName) ? null : StrictFolderNameCheckRegex().Match(folderName);
        var match = config.Mode is not Configuration.MatchMode.StrictAndFast
            ? ParsedFileResult.Match(filePath, _rules)
            : ParsedFileResult.Empty;
        if (match.AnidbAnimeId.HasValue)
            animeId = match.AnidbAnimeId.Value;
        else if (filenameMatch.Groups["animeId"].Success)
            animeId = int.Parse(filenameMatch.Groups["animeId"].Value.Trim());
        else if (folderNameMatch is { Success: true })
            animeId = int.Parse(folderNameMatch.Groups["animeId"].Value.Trim());
        if (filenameMatch.Groups["episodeRange"].Success)
            releaseInfo = (await GetReleaseInfoById(IdPrefix + filenameMatch.Groups["episodeRange"].Value, cancellationToken))!;
        else if (config.Mode is Configuration.MatchMode.Lax && match is { Success: true })
            releaseInfo = await GetReleaseInfoForMatch(match, animeId, cancellationToken);
        if (releaseInfo is not { CrossReferences.Count: > 0 })
            return null;

        if (releaseInfo.CrossReferences.Any(xref => xref.AnidbAnimeID is null or <= 0) && animeId is > 0 && _anidbService.SearchByID(animeId.Value) is not null)
        {
            foreach (var xref in releaseInfo.CrossReferences)
            {
                if (xref.AnidbAnimeID is null or <= 0)
                    xref.AnidbAnimeID = animeId;
            }
        }

        if (match is { Success: true })
        {
            var group = (ReleaseGroup?)null;
            if (!string.IsNullOrEmpty(match.ReleaseGroup))
            {
                if (config.MapAsAnidbGroupIfPossible)
                    group = OfflineReleaseGroupSearch.LookupByName(match.ReleaseGroup, _applicationPaths);
                group ??= new() { ID = match.ReleaseGroup, Name = match.ReleaseGroup, ShortName = match.ReleaseGroup, Source = "Offline" };
            }

            releaseInfo.Group = group;
            if (match.Source is not null)
                releaseInfo.Source = match.Source.Value;
            releaseInfo.OriginalFilename = Path.GetFileName(match.FilePath);
            releaseInfo.Version = match.Version ?? 1;
            releaseInfo.Metadata = JsonConvert.SerializeObject(match, new JsonSerializerSettings() { Converters = [new StringEnumConverter()] });
            releaseInfo.IsCreditless = match.Creditless;
            releaseInfo.IsCensored = match.Censored;
            // Assume the creation date has been properly set in the file-system.
            releaseInfo.ReleasedAt ??= DateOnly.FromDateTime(File.GetCreationTimeUtc(match.FilePath));
        }

        return releaseInfo;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatch(ParsedFileResult match, int? animeId, CancellationToken cancellationToken)
    {
        ReleaseInfo? releaseInfo;
        if (animeId is > 0)
        {
            _logger.LogDebug("Found anime ID in folder name to use: {AnimeID}.", animeId);
            if (_anidbService.SearchByID(animeId.Value) is not { } searchResult)
            {
                _logger.LogDebug("No search result found for anime ID {AnimeID}.", animeId);
                return null;
            }

            releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is not null)
            {
                _logger.LogDebug("Found anime id match for {ShowName} in search results.", match.SeriesName);
                return releaseInfo;
            }

            _logger.LogDebug("No match found for anime ID {AnimeID}.", animeId);
            return null;
        }

        var searchResults = _anidbService.Search(match.SeriesName!, fuzzy: true);
        if (searchResults.Count == 0)
        {
            _logger.LogDebug("No search results found for {ShowName}.", match.SeriesName);
            return null;
        }

        var followSeasonNumber = _configurationProvider.Load().AllowSeasonSearching &&
            match is { Year: null, SeasonNumber: > 1 and < 20, EpisodeType: EpisodeType.Episode, EpisodeStart: > 0, EpisodeEnd: > 0 };
        var limit = _configurationProvider.Load().MaxSearchResultsToProcess;
        _logger.LogDebug("Found {Count} search results for {ShowName}. (Year={Year},Type={AnimeType},Limit={Limit})", searchResults.Count, match.SeriesName, match.Year, match.SeriesType, limit);
        foreach (var searchResult in searchResults.Take(limit))
        {
            releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken, year: match.Year, animeType: match.SeriesType, followSeasonNumber: followSeasonNumber).ConfigureAwait(false);
            if (releaseInfo is not null)
            {
                _logger.LogDebug("Found match for {ShowName} in search results. (Anime={AnimeID})", match.SeriesName, searchResult.ID);
                return releaseInfo;
            }
        }

        _logger.LogDebug("No match found for {ShowName} in search results.", match.SeriesName);
        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatchAndAnime(ParsedFileResult match, IAnidbAnimeSearchResult searchResult, CancellationToken cancellationToken, int depth = 0, int? year = null, AnimeType? animeType = null, bool followSeasonNumber = false, DateTime? previousAirDate = null)
    {
        var anime = searchResult.AnidbAnime;
        if (
            anime is null ||
            (followSeasonNumber && anime.RelatedSeries.Count == 0) ||
            !anime.Episodes.Any(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
        )
        {
            var method = _configurationProvider.Load().AllowRemote
                ? AnidbRefreshMethod.Cache | AnidbRefreshMethod.Remote | AnidbRefreshMethod.SkipTmdbUpdate
                : AnidbRefreshMethod.Cache | AnidbRefreshMethod.SkipTmdbUpdate;
            _logger.LogDebug("Refreshing AniDB Anime {AnimeName} (Anime={AnimeID},Method={Method})", searchResult.DefaultTitle.Value, searchResult.ID, method.ToString());
            try
            {
                anime = await _anidbService.RefreshByID(searchResult.ID, method, cancellationToken).ConfigureAwait(false);
            }
            catch (AnidbHttpBannedException ex)
            {
                _logger.LogWarning(ex, "Got banned while refreshing {AnimeName} (Anime={AnimeID})", searchResult.DefaultTitle.Value, searchResult.ID);
                return null;
            }

            if (anime is null)
                return null;
        }

        // Forced match by custom rule.
        if (match.AnidbAnimeId.HasValue && match.AnidbEpisodeId.HasValue)
        {
            var episode = anime.Episodes.FirstOrDefault(x => x.ID == match.AnidbEpisodeId.Value);
            if (episode is null)
            {
                _logger.LogDebug("Given episode ID does not belong to the given anime ID. (Anime={AnimeID},Episode={EpisodeID})", anime.ID, match.AnidbEpisodeId);
                return null;
            }

            _logger.LogDebug("Found episode {EpisodeType} {EpisodeNumber} for {ShowName}. (Anime={AnimeID},Episode={EpisodeID})", episode.Type.ToString(), episode.EpisodeNumber, anime.DefaultTitle.Value, anime.ID, episode.ID);
            return new ReleaseInfo()
            {
                ID = $"{IdPrefix}{anime.ID}-{episode.ID}",
                CrossReferences = [new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = episode.ID }],
            };
        }

        // Switch season if we're in the wrong season.
        if (followSeasonNumber)
        {
            // We need an air date to do this, and if it doesn't have one then
            // it's most likely haven't aired yet.
            if (anime.AirDate is not { } currentAirDate)
            {
                _logger.LogDebug("Season number is set but anime {AnimeName} does not have an air date. (Anime={AnimeID})", searchResult.DefaultTitle.Value, searchResult.ID);
                return null;
            }

            // When looking for prequels/sequels, check the air date to prevent
            // following pre-sequels in either direction.
            if (previousAirDate.HasValue && (depth is 0 ? currentAirDate > previousAirDate.Value : currentAirDate < previousAirDate.Value))
            {
                _logger.LogDebug("Anime aired after previous air date. (Anime={AnimeID})", searchResult.ID);
                return null;
            }

            // If the entry anime have any prequels, then first go backwards to
            // find the earliest prequel before starting the forward search,
            // ignoring any pre-sequels.
            var relations = anime.RelatedSeries;
            if (depth is 0 && relations.Any(x => x is { RelationType: RelationType.Prequel }))
            {
                _logger.LogDebug("Attempting prequel(s) for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                var prequels = relations
                    .Where(x => x is { RelationType: RelationType.Prequel })
                    .OrderBy(x => x.RelatedID)
                    .ToList();
                var shouldContinue = true;
                foreach (var prequel in prequels)
                {
                    var prequelSearch = _anidbService.SearchByID(prequel.RelatedID);
                    if (prequelSearch is null)
                    {
                        _logger.LogDebug("Unknown prequel for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", anime.DefaultTitle.Value, anime.ID, prequel.RelatedID);
                        continue;
                    }

                    _logger.LogDebug("Attempting prequel {PrequelAnimeName} for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", prequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, prequelSearch.ID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, prequelSearch, cancellationToken, depth, year, animeType, followSeasonNumber, currentAirDate).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found prequel {PrequelAnimeName} for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", prequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, prequelSearch.ID);
                        return finalResult;
                    }

                    if (shouldContinue && prequelSearch.AnidbAnime is { } prequelAnime && prequelAnime.AirDate is { } prequelAirDate && prequelAirDate < currentAirDate)
                        shouldContinue = false;
                }

                if (!shouldContinue)
                {
                    _logger.LogDebug("No prequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                    return null;
                }
            }

            // Do the year/type check on the entry anime.
            if (depth is 0)
            {
                if (year.HasValue && (currentAirDate.Year != year.Value))
                {
                    _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, currentAirDate.Year, year);
                    return null;
                }

                if (animeType is not null && anime.Type != animeType)
                {
                    _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, anime.Type, animeType);
                    return null;
                }
            }

            // If the anime is a movie series or tv special, or if it's an OVA
            // with less then the cut-off number, then ignore it and look for
            // any the next sequel.
            const int OvaCutOff = 8;
            if (anime.Type is AnimeType.Movie or AnimeType.TVSpecial || (anime.Type is AnimeType.OVA && anime.EpisodeCounts[EpisodeType.Episode] <= OvaCutOff))
            {
                var sequels = relations
                    .Where(x => x is { RelationType: RelationType.Sequel })
                    .OrderBy(x => x.RelatedID)
                    .ToList();
                foreach (var sequel in sequels)
                {
                    var sequelSearch = _anidbService.SearchByID(sequel.RelatedID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.ID, sequel.RelatedID);
                        continue;
                    }

                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, sequelSearch, cancellationToken, depth, year, animeType, followSeasonNumber, previousAirDate).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                        return finalResult;
                    }
                }

                _logger.LogDebug("No sequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                return null;
            }

            // Unless we found the desired season number then continue until we
            // exhaust all sequels.
            if (depth + 1 != match.SeasonNumber)
            {
                var sequels = relations
                    .Where(x => x is { RelationType: RelationType.Sequel })
                    .OrderBy(x => x.RelatedID)
                    .ToList();
                foreach (var sequel in sequels)
                {
                    var sequelSearch = _anidbService.SearchByID(sequel.RelatedID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.ID, sequel.RelatedID);
                        continue;
                    }

                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, sequelSearch, cancellationToken, depth + 1, year, animeType, followSeasonNumber, currentAirDate).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                        return finalResult;
                    }
                }

                _logger.LogDebug("No sequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                return null;
            }
        }

        // Prevents following movies when searching for sequels, since we can't
        // know beforehand if the anidb anime is a movie before we potentially
        // fetch it.
        if (depth is > 0 && anime is { Type: AnimeType.Movie or AnimeType.Unknown })
        {
            _logger.LogDebug("Skipping unknown or movie {AnimeName} (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
            return null;
        }

        var allEpisodes = anime.Episodes.ToList();
        List<IAnidbEpisode> episodes;
        // Extra handling for TV Specials with multiple parts as additional episodes.
        if (match.EpisodeType is EpisodeType.Episode && anime.Type is AnimeType.TVSpecial && allEpisodes.Count > 0 && allEpisodes[0].DefaultTitle.Value is "TV Special" && allEpisodes.Count(ep => ep.Type is EpisodeType.Episode) > 1)
        {
            episodes = allEpisodes
                .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd + 1 && x.EpisodeNumber >= match.EpisodeStart + 1)
                .ToList();
            if (episodes.Count > 0)
            {
                if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
                {
                    _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, anime.AirDate?.Year, year);
                    return null;
                }

                if (animeType is not null && anime.Type != animeType)
                {
                    _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, anime.Type, animeType);
                    return null;
                }

                var parts = allEpisodes.Count(ep => ep.Type is EpisodeType.Episode) - 1;
                var range = (int)Math.Floor(100d / parts);
                var start = (int)(range * (match.EpisodeStart - 1));
                var rangeSize = match.EpisodeEnd - match.EpisodeStart + 1;
                var end = start + (int)(range * rangeSize);
                if (start + range > 95)
                    end = 100;
                return new ReleaseInfo()
                {
                    ID = IdPrefix + episodes.Select(x => $"{anime.ID}-{x.ID}").Join(','),
                    CrossReferences = [
                        new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = allEpisodes[0].ID, PercentageStart = start, PercentageEnd = end },
                        .. episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = x.ID }),
                    ],
                };
            }
        }
        // Extra handling for credits if we managed to parse the credit type
        else if (match.EpisodeType is EpisodeType.Credits && !string.IsNullOrWhiteSpace(match.EpisodeText))
        {
            var (type, num, suf) = ParseCreditType(match.EpisodeText);
            var allCredits = allEpisodes
                .Where(x => x.Type == EpisodeType.Credits)
                .Select(x => (x, y: ParseCreditType(x.DefaultTitle.Value)))
                .Where(t => t.y.type == type)
                .ToList();
            var altMode = allCredits.GroupBy(x => x.y.number).All(g => g.Count() == 1);
            episodes = allCredits
                .Select(t => (
                    t.x,
                    t.y,
                    z: altMode
                        ? new HashSet<string>([.. ParseCreditTitle(t.x.DefaultTitle.Value), .. ParseCreditTitle($"{type}{IntegerToSuffix(t.y.number)}")], StringComparer.InvariantCultureIgnoreCase)
                        : ParseCreditTitle(t.x.DefaultTitle.Value)
                ))
                .Where(t => t.z.Contains(match.EpisodeText))
                .Select(x => x.x)
                .ToList();
        }
        // HAMA-style specials offset handling.
        else if (
            match is { EpisodeStart: > 200, EpisodeEnd: > 200 } &&
            anime.EpisodeCounts is { } episodeCounts && (
                (match.EpisodeType is EpisodeType.Episode && episodeCounts.Episodes is > 0 and <= 190) ||
                (match.EpisodeType is EpisodeType.Special && episodeCounts.Specials is > 0 and <= 190)
            )
        )
        {
            var (episodeType, offset) = true switch
            {
                // The 100-200 range is not supported.
                _ when match.EpisodeStart is >= 201 and < 300 && episodeCounts.Trailers > 0 => (EpisodeType.Trailer, 200), // Trailers
                _ when match.EpisodeStart is >= 301 and < 400 && episodeCounts.Credits > 0 => (EpisodeType.Credits, 300), // OPs/EDs/etc.
                _ when match.EpisodeStart is >= 401 and < 500 && episodeCounts.Others > 0 => (EpisodeType.Other, 400), // Others
                _ => (match.EpisodeType, 0),
            };
            var episodeStart = match.EpisodeStart - offset;
            var episodeEnd = match.EpisodeEnd - offset;
            if (episodeType is EpisodeType.Special && episodeEnd - episodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
                episodes = SearchForSpecialByName(match.EpisodeName, episodeStart, allEpisodes);
            else
                episodes = allEpisodes
                    .Where(x => x.Type == episodeType && x.EpisodeNumber <= episodeEnd && x.EpisodeNumber >= episodeStart)
                    .ToList();
        }
        else if (match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeText))
        {
            episodes = SearchForSpecialByEpisodeText(match.EpisodeText, match.EpisodeStart, allEpisodes);
            if (episodes.Count is 0 && match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
                episodes = SearchForSpecialByName(match.EpisodeName, match.EpisodeStart, allEpisodes);
        }
        else if (match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
        {
            episodes = SearchForSpecialByName(match.EpisodeName, match.EpisodeStart, allEpisodes);
        }
        else
        {
            episodes = allEpisodes
                .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
                .ToList();
        }
        if (episodes.Count == 0)
        {
            var highestEpisodeNumber = allEpisodes.Count > 0 ? allEpisodes.Max(x => x.EpisodeNumber) : 0;
            if (match.EpisodeStart > highestEpisodeNumber)
            {
                _logger.LogDebug(
                    "Episode range ({EpisodeStart}-{EpisodeEnd}) is above last episode number ({LastEpisodeNumber}), trying to find sequel for {AnimeName}. (Anime={AnimeID})",
                    match.EpisodeStart,
                    match.EpisodeEnd,
                    highestEpisodeNumber,
                    anime.DefaultTitle.Value,
                    anime.ID
                );
                var sequels = anime.RelatedSeries.Where(x => x.RelationType == RelationType.Sequel)
                    .ToList();
                if (sequels.Count == 0)
                {
                    _logger.LogDebug("No sequels found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                    return null;
                }

                foreach (var sequel in sequels)
                {
                    var sequelSearch = _anidbService.SearchByID(sequel.RelatedID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.ID, sequel.RelatedID);
                        continue;
                    }

                    var sequelMatch = new ParsedFileResult()
                    {
                        Success = true,
                        FilePath = match.FilePath,
                        FileExtension = match.FileExtension,
                        EpisodeName = match.EpisodeName,
                        EpisodeEnd = match.EpisodeEnd - highestEpisodeNumber,
                        EpisodeStart = match.EpisodeStart - highestEpisodeNumber,
                        EpisodeType = match.EpisodeType,
                        ReleaseGroup = match.ReleaseGroup,
                        SeasonNumber = match.SeasonNumber,
                        SeriesName = match.SeriesName,
                        Version = match.Version,
                        RuleName = match.RuleName,
                    };
                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                    var sequelResult = await GetReleaseInfoForMatchAndAnime(sequelMatch, sequelSearch, cancellationToken, depth + 1, year, animeType, followSeasonNumber, previousAirDate).ConfigureAwait(false);
                    if (sequelResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.ID, sequelSearch.ID);
                        return sequelResult;
                    }
                }

                _logger.LogDebug("No matched sequels found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
                return null;
            }

            _logger.LogDebug("No episodes found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.ID);
            return null;
        }

        if (!followSeasonNumber)
        {
            if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
            {
                _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, anime.AirDate?.Year, year);
                return null;
            }

            if (animeType is not null && anime.Type != animeType)
            {
                _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.ID, anime.Type, animeType);
                return null;
            }
        }

        foreach (var episode in episodes)
        {
            _logger.LogDebug("Found episode {EpisodeType} {EpisodeNumber} for {ShowName}. (Anime={AnimeID},Episode={EpisodeID})", episode.Type.ToString(), episode.EpisodeNumber, anime.DefaultTitle.Value, anime.ID, episode.ID);
        }
        var releaseInfo = new ReleaseInfo()
        {
            ID = IdPrefix + episodes.Select(x => $"{anime.ID}-{x.ID}").Join(','),
            CrossReferences = episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.ID, AnidbEpisodeID = x.ID }).ToList(),
        };
        return releaseInfo;
    }

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
    {
        if (releaseId.StartsWith(FilePrefix))
        {
            var filePath = releaseId[FilePrefix.Length..];
            var relativePath = Path.GetFileName(filePath);
            var folderName = "/" + Path.GetDirectoryName(filePath);
            if (folderName is { Length: > 0 })
                relativePath = "/" + folderName + relativePath;
            _logger.LogDebug("Getting release info for {FilePath} (RelativePath={RelativePath})", filePath, relativePath);
            return GetReleaseInfoByFilePath(filePath, relativePath, cancellationToken);
        }

        if (!releaseId.StartsWith(IdPrefix))
        {
            _logger.LogWarning("Invalid release id: {ReleaseId}", releaseId);
            return Task.FromResult<ReleaseInfo?>(null);
        }

        releaseId = releaseId[IdPrefix.Length..];
        var segmentRegex = SegmentRegex();
        var segments = releaseId
            .Split(',')
            .Select(part => segmentRegex.Match(part))
            .ToList();
        if (segments.Count is 0 || segments.Any(part => part is null || !part.Success))
        {
            _logger.LogWarning("Malformed release id: {ReleaseId}", releaseId);
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

        return Task.FromResult<ReleaseInfo?>(new() { ID = IdPrefix + releaseId, CrossReferences = crossReferences, });
    }

    private static string IntegerToSuffix(int number) => number switch
    {
        // from a to z
        _ when number is >= 1 and <= 26 => ((char)('a' + number - 1)).ToString(),
        _ => string.Empty,
    };

    private static (string type, int number, string suffix) ParseCreditType(string title)
    {
        var match = CreditTitleRegex().Match(title);
        if (!match.Success)
            return (string.Empty, 0, string.Empty);

        var type = match.Groups["type"].Value.ToLowerInvariant() switch
        {
            "ed" or "ending" or "credits" => "ED",
            "op" or "opening" or "intro" => "OP",
            _ => null,
        };
        if (type is null)
            return (string.Empty, 0, string.Empty);

        var number = match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value) : 1;
        var suffix = match.Groups["suffix"].Value;
        if (match.Groups["version"].Success)
            suffix = match.Groups["version"].Value switch
            {
                "1" => "a",
                "2" => "b",
                "3" => "c",
                _ => suffix,
            };

        string? episodeRange = null;
        foreach (var group in match.Groups.Values.Where(g => g.Name.StartsWith("episodeRange")))
        {
            if (group.Success)
            {
                episodeRange = group.Value;
                break;
            }
        }

        var realSuffix = !string.IsNullOrEmpty(suffix) ? suffix : !string.IsNullOrEmpty(episodeRange) ? $" ({episodeRange})" : string.Empty;
        return (type, number, realSuffix);
    }

    private static HashSet<string> ParseCreditTitle(string title)
    {
        var matches = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var match = CreditTitleRegex().Match(title);
        if (!match.Success)
            return matches;

        var types = match.Groups["type"].Value.ToLowerInvariant() switch
        {
            "ed" or "ending" or "credits" => new string[] { "ED", "ED ", "Ending ", "Credits " },
            "op" or "opening" or "intro" => ["OP", "OP ", "Opening ", "Intro "],
            _ => null,
        };
        if (types is null)
            return matches;

        var isSpecial = match.Groups["isSpecial1"].Success || match.Groups["isSpecial2"].Success;
        var isExtra = match.Groups["isExtra"].Success;
        var isFull = match.Groups["isFull1"].Success || match.Groups["isFull2"].Success;
        var isFinalEpisode = match.Groups["isFinalEpisode"].Success;
        var isDVD = match.Groups["isDVD"].Success;
        var number = match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value).ToString() : "1";
        var suffix = match.Groups["suffix"].Value;
        if (match.Groups["version"].Success)
            suffix = match.Groups["version"].Value switch
            {
                "1" => "a",
                "2" => "b",
                "3" => "c",
                _ => suffix,
            };

        string? episodeRange = null;
        foreach (var group in match.Groups.Values.Where(g => g.Name.StartsWith("episodeRange")))
        {
            if (group.Success)
            {
                episodeRange = group.Value;
                break;
            }
        }

        var realSuffix = !string.IsNullOrEmpty(suffix) ? suffix : !string.IsNullOrEmpty(episodeRange) ? $" ({episodeRange})" : string.Empty;
        foreach (var type in types)
        {
            var modifiedType = type;
            if (realSuffix.Length > 0 && realSuffix[0] == ' ' && type[^1] == ' ')
                modifiedType = type[0..^1];
            var phrases = new List<string>() {
                $"{modifiedType}{number}{realSuffix}",
            };
            if (number is "1")
            {
                phrases.Add($"{modifiedType}{realSuffix}");
            }
            if (isExtra)
            {
                phrases.Add($"{modifiedType.TrimEnd()} EX{number}{realSuffix}");
                phrases.Add($"{modifiedType.TrimEnd()} EX {number}{realSuffix}");
            }
            if (number is "1" && isExtra)
            {
                phrases.Add($"{modifiedType.TrimEnd()} EX{realSuffix}");
                phrases.Add($"{modifiedType.TrimEnd()} EX {realSuffix}");
            }
            foreach (var phrase in phrases)
            {
                matches.Add(phrase.TrimEnd());
                if (isFull)
                {
                    matches.Add(("Full " + phrase).TrimEnd());
                    matches.Add(phrase.TrimEnd() + " (LongVer.)");
                }
                if (isFinalEpisode)
                {
                    if (type is "ED" or "OP")
                    {
                        matches.Add(("FE " + phrase).TrimEnd());
                        matches.Add(("FE" + phrase).TrimEnd());
                    }
                    else
                    {
                        matches.Add(("Final Episode " + phrase).TrimEnd());
                    }
                }
            }
        }
        return matches;
    }

    private static List<IAnidbEpisode> SearchForSpecialByEpisodeText(string episodeText, float episodeNumber, IReadOnlyList<IAnidbEpisode> allEpisodes)
    {
        var exactName = $"Episode {episodeText}";
        var exactNameShort = $"E{episodeText}";
        var startsWith = $"Episode {episodeText} ";
        var startsWithShort = $"E{episodeText} ";
        var specials = allEpisodes.Where(x => x.Type is EpisodeType.Special).ToList();
        var match =
            specials.FirstOrDefault(x =>
                string.Equals(x.DefaultTitle.Value, exactName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.DefaultTitle.Value, exactNameShort, StringComparison.OrdinalIgnoreCase) ||
                x.DefaultTitle.Value.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase) ||
                x.DefaultTitle.Value.StartsWith(startsWithShort, StringComparison.OrdinalIgnoreCase) ||
                x.Titles.Any(y =>
                    string.Equals(y.Value, exactName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(y.Value, exactNameShort, StringComparison.OrdinalIgnoreCase) ||
                    y.Value.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase) ||
                    y.Value.StartsWith(startsWithShort, StringComparison.OrdinalIgnoreCase)
                )
            ) ??
            specials.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        return match is not null ? [match] : [];
    }

    private static List<IAnidbEpisode> SearchForSpecialByName(string episodeName, float episodeNumber, IReadOnlyList<IAnidbEpisode> allEpisodes)
    {
        episodeName = episodeName.Replace('`', '\'');

        var specials = allEpisodes.Where(x => x.Type is EpisodeType.Special).ToList();
        var match =
            specials.FirstOrDefault(x =>
                string.Equals(x.DefaultTitle.Value.Replace('`', '\''), episodeName, StringComparison.OrdinalIgnoreCase) ||
                x.Titles.Any(y => string.Equals(y.Value.Replace('`', '\''), episodeName, StringComparison.OrdinalIgnoreCase))
            ) ??
            specials.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        return match is not null ? [match] : [];
    }

    private bool CheckAutomaticRelease(string filePath, IReadOnlyList<IVideoFile> videoFiles, ReleaseInfo releaseInfo, Configuration config)
    {
        _logger.LogInformation("Checking rules for automatic match for {Path}", filePath);
        foreach (var rule in config.AutoMatchRules)
        {
            _logger.LogTrace("Checking rule {Rule}", rule);
            if (rule.LocationRules.Count > 0)
            {
                _logger.LogDebug("Checking {Count} location rules for {Rule}", rule.LocationRules.Count, rule.Name);

                var ruleMatched = false;
                foreach (var locationRule in rule.LocationRules)
                {
                    foreach (var l2 in videoFiles)
                    {
                        if (l2.ManagedFolderID != locationRule.ManagedFolderID)
                            continue;
                        if (string.IsNullOrEmpty(locationRule.RelativePath))
                        {
                            ruleMatched = true;
                            break;
                        }
                        var rulePrefix = PlatformUtility.NormalizePath(locationRule.RelativePath, stripLeadingSlash: true);
                        if (rulePrefix[0] != '/')
                            rulePrefix = $"/{rulePrefix}";
                        if (rulePrefix[^1] != '/')
                            rulePrefix = $"{rulePrefix}/";
                        if (!l2.RelativePath.StartsWith(rulePrefix, PlatformUtility.StringComparison))
                            continue;
                        ruleMatched = true;
                        break;
                    }
                    if (ruleMatched)
                        break;
                }
                if (!ruleMatched)
                {
                    _logger.LogDebug("Location rules did not match for {Rule}", rule.Name);
                    continue;
                }
            }
            if (rule.GroupRules.Count > 0)
            {
                if (releaseInfo.Group is not { } releaseGroup)
                {
                    _logger.LogDebug("Release info has no release group.");
                    continue;
                }

                _logger.LogDebug("Checking {Count} release group rules for {Rule}", rule.GroupRules.Count, rule.Name);

                var ruleMatched = false;
                foreach (var groupRule in rule.GroupRules)
                {
                    if (groupRule.Type is Configuration.AutoMatchRule.AutoMatchGroupRule.GroupType.AniDB)
                    {
                        if (groupRule.AnidbID is not > 0)
                            continue;
                        if (releaseGroup is not { Source: "AniDB", ID.Length: > 0 } || !int.TryParse(releaseGroup.ID, out var releaseGroupId))
                            continue;
                        if (releaseGroupId != groupRule.AnidbID)
                            continue;
                        ruleMatched = true;
                        break;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(groupRule.GroupSource) &&
                            !string.Equals(groupRule.GroupSource, releaseGroup.Source, groupRule.IsGroupSourceCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(groupRule.GroupID) &&
                            !string.Equals(groupRule.GroupID, releaseGroup.Source, groupRule.IsGroupIDCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(groupRule.GroupName) &&
                            !string.Equals(groupRule.GroupName, releaseGroup.Name, groupRule.IsGroupSourceCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(groupRule.GroupName, releaseGroup.ShortName, groupRule.IsGroupSourceCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        ruleMatched = true;
                        break;
                    }
                }
                if (!ruleMatched)
                {
                    _logger.LogDebug("Release group rules did not match for {Rule}", rule.Name);
                    continue;
                }
            }
            if (!string.IsNullOrWhiteSpace(rule.EpisodeRanges))
            {
                _logger.LogDebug("Checking episode ranges for {Rule}", rule.Name);

                if (!ReleaseWithinEpisodeRanges(releaseInfo, rule.EpisodeRanges))
                {
                    _logger.LogDebug("Cross-references are not within any of the defined episode ranges of {Rule}", rule.Name);
                    continue;
                }
            }
            return true;
        }

        return false;
    }

    private bool ReleaseWithinEpisodeRanges(ReleaseInfo info, string episodeRanges)
    {
        var ranges = ParseEpisodeRanges(episodeRanges);
        foreach (var xref in info.CrossReferences)
        {
            var isValid = false;
            if (_metadataService.GetEpisodeByProviderID(xref.AnidbEpisodeID, IMetadataService.ProviderName.AniDB) is not IAnidbEpisode anidbEpisode)
                return false;
            foreach (var (type, start, end) in ranges)
            {
                switch (type)
                {
                    case "Episode":
                    case "Special":
                    case "Credits":
                    case "Trailer":
                    case "Parody":
                    case "Other":
                        var episodeType = Enum.Parse<EpisodeType>(type, true);
                        if (anidbEpisode.Type != episodeType)
                            continue;
                        if (!start.HasValue || !end.HasValue || anidbEpisode.EpisodeNumber >= start && anidbEpisode.EpisodeNumber <= end)
                        {
                            isValid = true;
                            break;
                        }
                        break;
                    case "OP":
                    case "ED":
                        if (anidbEpisode.Type is not EpisodeType.Credits)
                            continue;
                        var parsedInfo = ParseCreditType(anidbEpisode.DefaultTitle.Value);
                        if (parsedInfo.type != type)
                            continue;
                        if (!start.HasValue || !end.HasValue || parsedInfo.number >= start && parsedInfo.number <= end)
                        {
                            isValid = true;
                            break;
                        }
                        break;
                }
                if (isValid)
                    break;
            }
            if (!isValid)
                return false;
        }
        return false;
    }

    private static IReadOnlyList<(string, int?, int?)> ParseEpisodeRanges(string? episodeRanges)
    {
        if (string.IsNullOrEmpty(episodeRanges) || EpisodeRangeRegex().Matches(episodeRanges) is not { Count: > 0 } result)
            return [];

        var ranges = new List<(string, int?, int?)>();
        foreach (var range in result)
        {
            var match = EpisodeRangeRegex().Match(range!.ToString()!);
            var type = match.Groups["type"].Value switch
            {
                "E" or "" => "Episode",
                "S" or "SP" => "Special",
                "C" => "Credits",
                "OP" => "OP",
                "T" or "PV" => "Trailer",
                "P" => "Parody",
                "O" => "Other",
                "ED" => "ED",
                _ => null,
            };
            if (type is null)
                continue;

            var start = match.Groups["isStar"].Success ? null : (int?)int.Parse(match.Groups["rangeStart"].Value);
            var end = start is null ? null : match.Groups["rangeEnd"].Success ? int.Parse(match.Groups["rangeEnd"].Value) : start;
            if (start.HasValue && end.HasValue && start > end)
                (start, end) = (end, start);
            ranges.Add((type, start, end));
        }
        return ranges;
    }

    [GeneratedRegex(@"^(?:(?<isFinalEpisode>Final Episode) |Episode (?<episodeRange1>\d+(?:\-\d+)?) |(?<isFull1>Full) |(?<isDVD>DVD) |(?<isSpecial1>Special|Bonus|Special Broadcast|TV)? |(?<isCreditless>creditless) |(?<isInternational>international) |(?<isOriginal>Original )?(?<language>japanese|english|american|us|german|french(?: uncensored)?|italian|spanish|arabic) |(?<isEdgeCase>BD-BOX II|Doukyuusei: Natsu no Owari ni|Ami-chan's First Love|Narration -) )?(?<type>ED|OP|Opening|Ending|Intro|Credits)(?<isExtra> EX)?(?: *(?<number>\d+))?(?<suffix> ?[a-z]|\.\d+)?(?:(?: \((?<episodeRange2>\d+(?:\-\d+)?)\)| Episode (?<episodeRange3>\d+(?:\-\d+)?))?| \((?:(?<isSpecial2>Specials?)|Episode (?<episodeRange4>S?\d+(?:\-\d+)?(?: ?& ?S?\d+(?:\-\d+)?)?)|(?<isFull2>LongVer.)|(?<details1>[^\)]+))\)| \[(?<details2>[^\]]+)\]| (?<isFull3>full)| - Version (?<version>\d+)| - .+| .+)?$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreditTitleRegex();

    [GeneratedRegex(@"\[(?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\]|,),?)+\]|\((?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\)|,),?)+\)|\{(?<anidb>anidb(?:[\- \.]?id)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?<animeId>\d+)\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFolderNameCheckRegex();

    [GeneratedRegex(@"\[\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\]|,),?)+\]|\(\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\)|,),?)+\)|\{\s*(?<anidb>anidb(?:[\- \.]?\s*ids?)?)[\-= ](?:(?<=\k<anidb>[\-= ]|,)\s*(?:(?<episodeRange>(?:a?\d+(?:[=\- \.]|(?=e)))?e?\d+(?:['@]\d+(?:\-\d+)?%?)?)|a(?<animeId>\d+))\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFilenameCheckRegex();

    [GeneratedRegex(@"(?<=^|,)\s*(?:a?(?<animeId>\d+)(?:[=\- \.]|(?=e)))?e?(?<episodeId>\d+)(?:['@](?<percentRangeStartOrWholeRange>\d+)(?:\-(?<percentRangeEnd>\d+))?%?)?\s*(?=$|,),?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();

    [GeneratedRegex(@"(?<!\-)(?:\b|(?=[\*|E|SP?|C|OP|ED|P|T|PV|O]))(?<prefix>(E|SP?|C|OP|ED|P|T|PV|O))?(?:(?<isStar>\*)|(?:(?<rangeStart>[1-9][0-9]*)(?:-\k<prefix>?(?<rangeEnd>[1-9][0-9]*)?)?))[,\.]?(?:(?<=[\*|E|SP?|C|OP|ED|P|T|PV|O])|\b)")]
    private static partial Regex EpisodeRangeRegex();

    /// <summary>
    /// Configure how the offline importer works.
    /// </summary>
    [Section(DisplaySectionType.Minimal)]
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
        /// If disabled, the importer will not attempt to use the season number
        /// to search for the release.
        /// </summary>
        [Display(Name = "Enable season searching")]
        [DefaultValue(true)]
        public bool AllowSeasonSearching { get; set; } = true;

        /// <summary>
        /// The maximum number of search results to process.
        /// </summary>
        [Display(Name = "Maximum search results to process")]
        [DefaultValue(1)]
        [Range(1, 100)]
        public int MaxSearchResultsToProcess { get; set; } = 1;

        /// <summary>
        /// If enabled, the importer will skip the availability check and
        /// check all video file paths regardless of availability.
        /// </summary>
        [Badge("Debug", Theme = DisplayColorTheme.Warning)]
        [Visibility(Advanced = true)]
        public bool SkipAvailabilityCheck { get; set; }

        /// <summary>
        /// The regex patterns to use for matching.
        /// </summary>
        [Display(Name = "Rules")]
        [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
        [Visibility(Advanced = true)]
        [List(ListType = DisplayListType.ComplexInline, UniqueItems = true)]
        public List<CustomRuleDefinition> Rules { get; set; } = [
            new()
            {
                Name = "anti-timestamp",
                Regex = @"^\d{4}[._:\- ]\d{2}[._:\- ]\d{2}[._:\- T]\d{2}[._:\- ]\d{2}[._:\- ]\d{2}(?:[._:\- ]\d{1,6})?(?:Z|[+-]\d{2}:?\d{2})?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                Type = CustomRuleType.Deny,
            },
            new()
            {
                Name = "trash-anime",
                Regex = @"^(?<showName>.+?(?: \((?<year>\d{4})\))) - (?:(?<isSpecial>S00?)|S\d+)E\d+(?:-E?\d+)? - (?<episode>\d+(?:-\d+)?) - (?<episodeName>.+?(?=\[)).*?(?:-(?<releaseGroup>[^\[\] ]+))?\s*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            },
            new()
            {
                Name = "default",
                Regex = @"^(?<pre>.*?(?<!(?:PV|CM|Preview|Trailer|Commercial|Menu|Jump Festa Special)[\._ ]*(-[\._ ]*)?))[\._ ]*(?:-[\._ ]*)?(?<=\b|_)(?:(?:s(?<season>\d+(?!\d+)))?(?:(?:OVA|OAD)[\._ ]*|Vol(?:\.|ume)[\._ ]*|(?<=\b|_)(?<isOther>o)|e(?:p(?:isode|s)?)?[\._ ]*|(?=(?:\d+(?:(?!-?\d+[pi])-\d+?|\.5)?)(?:v\d{1,2})?(?:[\._ ]*END(?=\b|_))?[\._ ]*\.[a-z0-9]{1,10}$)|(?<=[\._ ]*-[\._ ]*)(?=(?:\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?:[\._ ]*END(?=\b|_))?[\._ ]*(?:[\._ ]*-[\._ ]*|[\[\({「]))|(?<=[\._ ]*-[\._ ]*)(?=(?:(?!(?:19|2[01])\d{2})\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?![\._ ]*-[\._ ]*))|(?=(?:(?!\d+[\. _]\(\d{4}\))\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?:v\d{1,2})?(?:[\._ ]*END(?=\b|_))?[\._ ]*[\[\({「])|(?<=s\d+) )(?<episode>\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?!(?:\.\d)?(?:\]|\))|-(?!\d+[pi])\d+|(?<!(?:e(?:pisode|ps?)?)?\k<episode>)[\._ ]*(?:OVA|OAD)| (?:nc)?(?:ed|op))|(?<!jump[\. _]festa[\. _])s(?:p(?:ecials?)?)?[ \.]?(?<specialNumber>\d+(?!(?:\.\d)?(?:\]|\))| \d+|[\. _]?-[\. _]?(?:E(?:p(?:isode)?)?[\. _]?)?\d+))(?![\. _-]*(?:nc)?(?:ed|op))|(?<=\b|_)(?<![a-z0-9])(?:s(?<season>\d+(?!\d+))(?:[\. _]*-)?[\. _]*)?(?:(?<isCreditless>nc|[Cc]reditless|NC)[\s_.]*)?(?<isThemeSong>ED|OP|Opening|Ending)(?![a-z]{2,})(?:[\._ ]*(?<episode>\d+(?!\d*p)))?(?<themeSuffix>(?<=(?:OP|ED)(?:[\._ ]*\d+)?)(?:\.\d+|[a-z]))?(?=\b|_))(?:v(?<version>\d{1,2}))?(?=\b|_)(?:[\._ ]*END)?[\._ ]*(?:-[\._ ]*)?(?<post>.+)?\.(?<extension>[a-z0-9]{1,10})$",
                Type = CustomRuleType.PrePost,
            },
            new()
            {
                Name = "brackets-1",
                Regex = @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<year>\d{4})\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            },
            new()
            {
                Name = "brackets-2",
                Regex = @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            },
            new()
            {
                Name = "reversed-1",
                Regex = @"^\[?(?<episode>\d+)\s*-\s*(?<showName>[^[]+)\]\s*(?:\[[^\]]*\])*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            },
            new()
            {
                Name = "fallback",
                Regex = @"^(?<fallback>.+)\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                Type = CustomRuleType.Fallback,
            },
        ];

        /// <summary>
        /// Determines how the provider acts in an auto-matching scenario.
        /// </summary>
        [Display(Name = "Auto Match Mode")]
        public AutoMatchMode AutoMatch { get; set; } = AutoMatchMode.Disabled;

        /// <summary>
        /// Auto matching rules.
        /// </summary>
        [List(ListType = DisplayListType.ComplexInline)]
        public List<AutoMatchRule> AutoMatchRules { get; set; } = [];

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

        /// <summary>
        /// The type of a custom rule.
        /// </summary>
        public enum CustomRuleType
        {
            /// <summary>
            /// Default rule type.
            /// </summary>
            [Display(Name = "Default Match")]
            [EnumMember(Value = "default")]
            [Key]
            Default = 0,

            /// <summary>
            /// Pre-post filtering rule. Has extra logic to handle "post"/"pre"
            /// groups in the match.
            /// </summary>
            /// <remarks>
            /// Will be applied on top of the default rule.
            /// </remarks>
            [Display(Name = "Pre/Post Match")]
            [EnumMember(Value = "pre-post")]
            PrePost = 1,

            /// <summary>
            /// Fallback rule. Has extra logic to handle missing episode
            /// numbers in the match.
            /// </summary>
            /// <remarks>
            /// Will be applied on top of the default rule.
            /// </remarks>
            [Display(Name = "Fallback Match (No Episode Number)")]
            [EnumMember(Value = "fallback")]
            Fallback = 2,

            /// <summary>
            /// Will deny all matches to this rule.
            /// </summary>
            [Display(Name = "Deny Match")]
            [EnumMember(Value = "deny")]
            Deny = 3,
        }

        /// <summary>
        /// Automatic matching mode.
        /// </summary>
        public enum AutoMatchMode
        {
            /// <summary>
            /// Auto matching is disabled.
            /// </summary>
            [Display(Name = "Disabled")]
            [EnumMember(Value = "disabled")]
            Disabled,

            /// <summary>
            /// Only results matching one or more auto-matching rules will be valid matches.
            /// </summary>
            [Display(Name = "Rules Only")]
            [EnumMember(Value = "rules-only")]
            RulesOnly,

            /// <summary>
            /// All results will be valid matches.
            /// </summary>
            [Display(Name = "Rules and Files")]
            [EnumMember(Value = "rules-and-files")]
            All,
        }

        /// <summary>
        /// Auto matching rule.
        /// </summary>
        public class AutoMatchRule
        {
            /// <inheritdoc/>
            [Visibility(DisplayVisibility.Hidden), Key]
            public KeyValuePair<string, string> Key
            {
                get => new(
                    $"Episode Ranges: {ParseEpisodeRanges(EpisodeRanges).Count}," +
                    $"Location Rules: {LocationRules.Count}," +
                    $"Release Group Rules: {GroupRules.Count}",
                    !string.IsNullOrEmpty(Name) ? Name : "New Auto Match Rule"
                );
                // no setter
                set { }
            }

            /// <summary>
            /// Human and machine friendly name of the rule.
            /// </summary>
            [Visibility(Size = DisplayElementSize.Full)]
            [Display(Name = "Rule Name")]
            [DefaultValue("match-rule-1")]
            [Required]
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// The episode ranges to match.
            /// Can be multiple ranges separated by commas.
            /// Space and other whitespace characters are ignored.
            /// For a single digit range, just use the number.
            /// If you want to define a range for a different episode type, then prefix the range with the episode type prefix. Episode Prefixes:
            /// "" or "E" → Episode,
            /// "S" or "SP" → Special,
            /// "C" → All Credits,
            /// "OP" → Opening,
            /// "ED" → Ending,
            /// "T" or "PV" → Trailer,
            /// "P" → Parody,
            /// "O" → Other.
            /// Unprefixed ranges counts as normal episodes.
            /// If you want to match all episodes for a range, use the episode type prefix plus a star, e.g. "*", "E*", "S*", etc.
            /// Leave blank to allow everything.
            /// </summary>
            [Visibility(Size = DisplayElementSize.Full)]
            [DefaultValue("")]
            public string EpisodeRanges { get; set; } = string.Empty;

            /// <summary>
            /// 
            /// </summary>
            [List(ListType = DisplayListType.ComplexInline, Sortable = false)]
            public List<AutoMatchLocationRule> LocationRules { get; set; } = [];
            /// <summary>
            /// 
            /// </summary>
            [List(ListType = DisplayListType.ComplexInline, Sortable = false)]
            public List<AutoMatchGroupRule> GroupRules { get; set; } = [];

            /// <summary>
            /// A location auto-match rule.
            /// </summary>
            public class AutoMatchLocationRule
            {
                /// <inheritdoc/>
                [Visibility(DisplayVisibility.Hidden), Key]
                public KeyValuePair<string, string> Key
                {
                    get => new(
                        ManagedFolderID > 0 ? !string.IsNullOrEmpty(RelativePath) ? $"Relative Path: {RelativePath}" : "-" : "",
                        ManagedFolderID > 0
                            ? $"Managed Folder: {ManagedFolderName} ({ManagedFolderID})"
                            : ManagedFolderSelector is not null && ManagedFolderSelector.Options.FirstOrDefault(o => o.IsSelected) is { } selected ?
                                $"Managed Folder: {selected.Label} ({selected.Value})"
                            : "New Location Rule"
                    );
                    // no setter
                    set { }
                }

                /// <summary>
                /// The managed folder to match against.
                /// </summary>
                [Visibility(DisplayVisibility.Hidden)]
                public int ManagedFolderID { get; set; }

                /// <summary>
                /// The managed folder to match against.
                /// </summary>
                [Display(Name = "Managed Folder")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    Visibility = DisplayVisibility.ReadOnly,
                    ToggleVisibilityTo = DisplayVisibility.Hidden,
                    ToggleWhenMemberIsSet = nameof(ManagedFolderID),
                    ToggleWhenSetTo = 0
                )]
                public string? ManagedFolderName { get; set; }

                /// <summary>
                /// The managed folder to match against.
                /// </summary>
                [Display(Name = "Managed Folder")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    Visibility = DisplayVisibility.Visible,
                    ToggleVisibilityTo = DisplayVisibility.Hidden,
                    ToggleWhenMemberIsSet = nameof(ManagedFolderSelector),
                    ToggleWhenSetTo = null,
                    InverseToggleCondition = true
                )]
                public SelectComponent<int>? ManagedFolderSelector { get; set; }

                /// <summary>
                /// Relative path prefix from the start of the managed folder to match against.
                /// </summary>
                [Visibility(
                    Size = DisplayElementSize.Full,
                    Visibility = DisplayVisibility.ReadOnly,
                    ToggleVisibilityTo = DisplayVisibility.Visible,
                    ToggleWhenMemberIsSet = nameof(ManagedFolderID),
                    ToggleWhenSetTo = 0
                )]
                [DefaultValue("")]
                public string RelativePath { get; set; } = string.Empty;

                /// <summary>
                ///   Live edit action handler.
                /// </summary>
                /// <param name="context">The context for the action.</param>
                /// <param name="service">The video service.</param>
                /// <returns>The result of the action.</returns>
                [ConfigurationAction(ConfigurationActionType.LiveEdit)]
                public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext<Configuration> context, IVideoService service)
                {
                    if (ManagedFolderID is 0 && ManagedFolderSelector is null)
                    {
                        try
                        {
                            ManagedFolderSelector = new SelectComponent<int>(
                                service.GetAllManagedFolders().Select(folder => new SelectOption<int>(folder.ID, folder.Name))
                            );
                        }
                        catch { }
                    }
                    return new(context.Configuration);
                }
            }

            /// <summary>
            /// A release group auto-match rule.
            /// </summary>
            public class AutoMatchGroupRule
            {
                /// <inheritdoc/>
                [Visibility(DisplayVisibility.Hidden), Key]
                public KeyValuePair<string, string> Key
                {
                    get => new(
                        Type switch
                        {
                            GroupType.AniDB => "AniDB",
                            GroupType.Custom => "Custom" + (string.IsNullOrEmpty(GroupSource) ? "" : $" ({GroupSource})"),
                            _ => string.Empty,
                        },
                        Type switch
                        {
                            GroupType.AniDB => OfflineReleaseGroupSearch.LookupByID(AnidbID) is { } group
                                ? $"Group: {group.Name} ({group.ID})"
                                : AnidbID is > 0 ? $"Group: <unknown> ({AnidbID})"
                                : "New Group Rule",
                            GroupType.Custom => !string.IsNullOrEmpty(GroupName ?? GroupID)
                                ? $"Group: {GroupName ?? GroupID}" + (string.IsNullOrEmpty(GroupID) ? "" : $" ({GroupID})")
                                : "New Group Rule",
                            _ => "New Group Rule",
                        }
                    );
                    // no setter
                    set { }
                }

                /// <summary>
                /// The type of group.
                /// </summary>
                public GroupType Type { get; set; }

                /// <summary>
                /// The ID of the AniDB group to match.
                /// </summary>
                [Display(Name = "AniDB Group ID")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.AniDB,
                    InverseDisableCondition = true
                )]
                public int AnidbID { get; set; }

                /// <summary>
                /// The ID of the custom group to match.
                /// </summary>
                [Display(Name = "Custom Group ID")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public string? GroupID { get; set; }

                /// <summary>
                /// Determines if the group ID is case sensitive.
                /// </summary>
                [Display(Name = "Custom Group ID Case Sensitive")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public bool IsGroupIDCaseSensitive { get; set; }

                /// <summary>
                /// The long or short name of the custom group to match.
                /// </summary>
                [Display(Name = "Custom Group Name")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public string? GroupName { get; set; }

                /// <summary>
                /// Determines if the group name is case sensitive.
                /// </summary>
                [Display(Name = "Custom Group Name Case Sensitive")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public bool IsGroupNameCaseSensitive { get; set; }

                /// <summary>
                /// The source of the custom group to match.
                /// </summary>
                [Display(Name = "Custom Group Source")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public string? GroupSource { get; set; }

                /// <summary>
                /// Determines if the group source is case sensitive.
                /// </summary>
                [Display(Name = "Custom Group Source Case Sensitive")]
                [Visibility(
                    Size = DisplayElementSize.Full,
                    DisableWhenMemberIsSet = nameof(Type),
                    DisableWhenSetTo = GroupType.Custom,
                    InverseDisableCondition = true
                )]
                public bool IsGroupSourceCaseSensitive { get; set; }

                /// <summary>
                /// The type of group to validate a rule against a potential match.
                /// </summary>
                public enum GroupType
                {
                    /// <summary>
                    /// Matches against an AniDB group.
                    /// </summary>
                    [Display(Name = "AniDB Group")]
                    [EnumMember(Value = "anidb")]
                    AniDB,

                    /// <summary>
                    /// Matches against a custom group.
                    /// </summary>
                    [Display(Name = "Custom Group")]
                    [EnumMember(Value = "custom")]
                    Custom,
                }

                /// <summary>
                ///   Live edit action handler.
                /// </summary>
                /// <param name="context">The context for the action.</param>
                /// <param name="applicationPaths">The application paths to use for storing the offline data.</param>
                /// <returns>The result of the action.</returns>
                [ConfigurationAction(ConfigurationActionType.LiveEdit)]
                public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext<Configuration> context, IApplicationPaths applicationPaths)
                {
                    OfflineReleaseGroupSearch.InitializeBeforeUse(applicationPaths);
                    return new(context.Configuration);
                }
            }

            /// <summary>
            ///   Live edit action handler.
            /// </summary>
            /// <param name="context">The context for the action.</param>
            /// <returns>The result of the action.</returns>
            [ConfigurationAction(ConfigurationActionType.LiveEdit)]
            public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext<Configuration> context)
            {
                foreach (var rule in LocationRules)
                {
                    if (rule.ManagedFolderSelector is not null)
                    {
                        if (rule.ManagedFolderSelector.Options.FirstOrDefault(o => o.IsSelected) is { } selected)
                        {
                            rule.ManagedFolderID = selected.Value;
                            rule.ManagedFolderName = selected.Label;
                        }
                        else
                        {
                            rule.ManagedFolderID = 0;
                            rule.ManagedFolderName = string.Empty;
                        }
                        rule.ManagedFolderSelector = null;
                    }
                }
                return new(context.Configuration);
            }
        }

        /// <summary>
        /// A custom rule definition.
        /// </summary>
        public class CustomRuleDefinition
        {
            /// <summary>
            /// Human and machine friendly name of the rule.
            /// </summary>
            [Visibility(Size = DisplayElementSize.Full)]
            [Display(Name = "Rule Name")]
            [DefaultValue("custom-rule-1")]
            [Key, Required]
            public string Name { get; set; } = "custom-rule-1";

            /// <summary>
            /// Determines the transform to apply post-matching.
            /// </summary>
            [Display(Name = "Type")]
            [Required]
            [DefaultValue(CustomRuleType.Default)]
            public CustomRuleType Type { get; set; } = CustomRuleType.Default;

            /// <summary>
            /// The episode type to forcefully use.
            /// </summary>
            [Display(Name = "Forced Episode Type")]
            [DefaultValue(null)]
            public EpisodeType? ForcedEpisodeType { get; set; } = null;

            /// <summary>
            /// The episode number to forcefully use.
            /// </summary>
            [Display(Name = "Forced Episode Number")]
            [DefaultValue(null)]
            public int? ForcedEpisodeNumber { get; set; } = null;

            /// <summary>
            /// The AniDB anime ID to forcefully use.
            /// </summary>
            [Display(Name = "Forced AniDB Anime ID")]
            [DefaultValue(null)]
            public int? ForcedAnidbAnimeId { get; set; }

            /// <summary>
            /// The AniDB episode ID to forcefully use.
            /// </summary>
            [Display(Name = "Forced AniDB Episode ID")]
            [DefaultValue(null)]
            public int? ForcedAnidbEpisodeId { get; set; }

            /// <summary>
            /// If enabled, the regex will be case sensitive.
            /// </summary>
            [Display(Name = "Case Sensitive")]
            [Required]
            [DefaultValue(false)]
            public bool IsCaseSensitive { get; set; } = false;

            /// <summary>
            /// Indicates that the regex should be applied to the file path instead of the file name.
            /// </summary>
            [Display(Name = "Use File Path")]
            [Required]
            [DefaultValue(false)]
            public bool UsePath { get; set; } = false;

            /// <summary>
            /// The regex to match against the file name.
            /// </summary>
            [TextArea]
            [Visibility(Size = DisplayElementSize.Large)]
            [Display(Name = "Regular Expression")]
            [Required]
            [DefaultValue("")]
            [StringSyntax("Regex")]
            public string Regex { get; set; } = string.Empty;

            /// <summary>
            /// Converts the custom rule definition to a match rule.
            /// </summary>
            /// <returns></returns>
            public ParsedFileResult.CompiledRule ToMatchRule() => new()
            {
                Name = Name,
                UsePath = UsePath,
                Regex = new(
                    Regex,
                    IsCaseSensitive
                        ? RegexOptions.ECMAScript | RegexOptions.Compiled
                        : RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                Transform = Transform,
            };

            ParsedFileResult? Transform(ParsedFileResult originalDetails, Match match)
            {
                Func<ParsedFileResult, Match, ParsedFileResult?> builtInTransform = Type switch
                {
                    CustomRuleType.Default => ParsedFileResult.DefaultTransform,
                    CustomRuleType.PrePost => ParsedFileResult.PrePostTransform,
                    CustomRuleType.Fallback => ParsedFileResult.FallbackTransform,
                    CustomRuleType.Deny => ParsedFileResult.DenyTransform,
                    _ => ParsedFileResult.DefaultTransform,
                };
                var result = builtInTransform(originalDetails, match);
                if (result == null) return null;
                if (ForcedEpisodeType.HasValue) result.EpisodeType = ForcedEpisodeType.Value;
                if (ForcedEpisodeNumber.HasValue) result.EpisodeStart = result.EpisodeEnd = ForcedEpisodeNumber.Value;
                if (ForcedAnidbAnimeId.HasValue) result.AnidbAnimeId = ForcedAnidbAnimeId.Value;
                if (ForcedAnidbEpisodeId.HasValue) result.AnidbEpisodeId = ForcedAnidbEpisodeId.Value;
                return result;
            }
        }
    }
}
