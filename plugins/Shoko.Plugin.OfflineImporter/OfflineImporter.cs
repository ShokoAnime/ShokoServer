
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

    /// <inheritdoc/>
    public Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken)
    {
        var folderRegex = StrictFolderNameCheckRegex();
        var filenameRegex = StrictFilenameCheckRegex();
        var config = configurationProvider.Load();
        logger.LogDebug("Getting release info for {Video}", video.ID);
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

            var animeId = (int?)null;
            var releaseInfo = (ReleaseInfo?)null;
            var parts = location.RelativePath[1..].Split('/');
            var (folderName, fileName) = parts.Length > 1 ? (parts[^2], parts[^1]) : (null, parts[0]);
            var filenameMatch = filenameRegex.Match(fileName);
            var folderNameMatch = string.IsNullOrEmpty(folderName) ? null : folderRegex.Match(folderName);
            var match = config.Mode is not Configuration.MatchMode.StrictAndFast
                ? MatchRuleResult.Match(filePath)
                : MatchRuleResult.Empty;
            if (folderNameMatch is { Success: true })
                animeId = int.Parse(folderNameMatch.Value[6..^1].Trim());
            if (filenameMatch.Success)
                releaseInfo = (await GetReleaseInfoById(IdPrefix + filenameMatch.Value, cancellationToken))!;
            else if (config.Mode is Configuration.MatchMode.Lax && match is { Success: true })
                releaseInfo = await GetReleaseInfoByFileName(match, animeId, cancellationToken);
            if (releaseInfo is not { CrossReferences.Count: > 0 })
                continue;

            if (releaseInfo.CrossReferences.Any(xref => xref.AnidbAnimeID is null or <= 0) && animeId is > 0 && anidbService.SearchByID(animeId.Value) is not null)
            {
                foreach (var xref in releaseInfo.CrossReferences)
                {
                    if (xref.AnidbAnimeID is null or <= 0)
                        xref.AnidbAnimeID = animeId;
                }
            }

            releaseInfo.FileSize = video.Size;
            releaseInfo.Hashes = video.Hashes.Hashes
                .Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata })
                .ToList();

            if (video.MediaInfo is { } mediaInfo)
            {
                if (mediaInfo.Encoded is { } encodedAt && encodedAt > DateTime.MinValue && encodedAt != DateTime.UnixEpoch)
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

                releaseInfo.Group = group;
                if (match.Source is not null)
                    releaseInfo.Source = match.Source.Value;
                releaseInfo.OriginalFilename = Path.GetFileName(match.FilePath);
                releaseInfo.Revision = match.Version ?? 1;
                releaseInfo.Metadata = JsonConvert.SerializeObject(match);
                releaseInfo.IsCreditless = match.Creditless;
                releaseInfo.IsCensored = match.Censored;
                // Assume the creation date has been properly set in the file-system.
                releaseInfo.ReleasedAt ??= DateOnly.FromDateTime(File.GetCreationTimeUtc(match.FilePath));
            }

            return releaseInfo;
        }

        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoByFileName(MatchRuleResult match, int? animeId, CancellationToken cancellationToken)
    {
        var releaseInfo = (ReleaseInfo?)null;
        if (animeId is > 0)
        {
            logger.LogDebug("Found anime ID in folder name to use: {AnimeID}", animeId);
            if (anidbService.SearchByID(animeId.Value) is not { } searchResult)
            {
                logger.LogDebug("No search result found for anime ID {AnimeID}", animeId);
                return null;
            }

            releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken, year: match.Year).ConfigureAwait(false);
            if (releaseInfo is not null)
            {
                logger.LogDebug("Found anime id match for {ShowName} in search results", match.ShowName);
                return releaseInfo;
            }

            logger.LogDebug("No match found for anime ID {AnimeID}", animeId);
            return null;
        }

        logger.LogDebug("Found potential match {ShowName} for {FileName}", match.ShowName, match.FilePath);
        if (anidbService.Search(match.ShowName!) is not { Count: > 0 } searchResults)
            searchResults = anidbService.Search(match.ShowName!, fuzzy: true);
        if (searchResults.Count == 0)
        {
            logger.LogDebug("No search results found for {ShowName}", match.ShowName);
            return null;
        }

        if (match.Year.HasValue)
        {
            logger.LogDebug("Found {Count} search results for {ShowName} with year {Year}", searchResults.Count, match.ShowName, match.Year);
            foreach (var searchResult in searchResults)
            {
                releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken, year: match.Year).ConfigureAwait(false);
                if (releaseInfo is not null)
                {
                    logger.LogDebug("Found year match for {ShowName} in search results", match.ShowName);
                    return releaseInfo;
                }
            }

            logger.LogDebug("No match found for {ShowName} in search results", match.ShowName);
            return null;
        }

        logger.LogDebug("Found {Count} search results for {ShowName0}, picking first one; {ShowName1}", searchResults.Count, match.ShowName, searchResults[0].DefaultTitle);
        releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResults[0], cancellationToken).ConfigureAwait(false);
        if (releaseInfo is not null)
            logger.LogDebug("Found match for {ShowName} in search results", match.ShowName);
        else
            logger.LogDebug("No match found for {ShowName} in search results", match.ShowName);
        return releaseInfo;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatchAndAnime(MatchRuleResult match, IAnidbAnimeSearchResult searchResult, CancellationToken cancellationToken, int depth = 0, int? year = null)
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
                    logger.LogWarning(ex, "Got banned while refreshing {AnimeName} (Anime={AnimeID})", searchResult.DefaultTitle, searchResult.ID);
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
            logger.LogDebug("Skipping unknown or movie {AnimeName} (Anime={AnimeID})", anime.DefaultTitle, anime.ID);
            return null;
        }

        var allEpisodes = anime.Episodes.ToList();
        List<IAnidbEpisode> episodes;
        // Extra handling for TV Specials with multiple parts as additional episodes.
        if (match.EpisodeType is EpisodeType.Episode && anime.Type is AnimeType.TVSpecial && allEpisodes.Count > 0 && allEpisodes[0].DefaultTitle is "TV Special" && allEpisodes.Count(ep => ep.Type is EpisodeType.Episode) > 1)
        {
            episodes = allEpisodes
                .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd + 1 && x.EpisodeNumber >= match.EpisodeStart + 1)
                .ToList();
            if (episodes.Count > 0)
            {
                if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
                {
                    logger.LogDebug("Year mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.ShowName, anime.DefaultTitle, anime.ID, anime.AirDate?.Year, year);
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
                .Select(x => (x, y: ParseCreditType(x.DefaultTitle)))
                .Where(t => t.y.type == type)
                .ToList();
            var altMode = allCredits.GroupBy(x => x.y.number).All(g => g.Count() == 1);
            episodes = allCredits
                .Select(t => (
                    t.x,
                    t.y,
                    z: altMode
                        ? new HashSet<string>([.. ParseCreditTitle(t.x.DefaultTitle), .. ParseCreditTitle($"{type}{IntegerToSuffix(t.y.number)}")], StringComparer.InvariantCultureIgnoreCase)
                        : ParseCreditTitle(t.x.DefaultTitle)
                ))
                .Where(t => t.z.Contains(match.EpisodeText))
                .Select(x => x.x)
                .ToList();
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
                logger.LogDebug("Found episode range is above last episode, trying to find sequel for {AnimeName} (Anime={AnimeID})", anime.DefaultTitle, anime.ID);
                var sequels = anime.RelatedSeries.Where(x => x.RelationType == RelationType.Sequel)
                    .ToList();
                if (sequels.Count == 0)
                {
                    logger.LogDebug("No sequels found for {AnimeName} (Anime={AnimeID})", anime.DefaultTitle, anime.ID);
                    return null;
                }

                foreach (var sequel in sequels)
                {
                    var sequelSearch = anidbService.SearchByID(sequel.RelatedID);
                    if (sequelSearch is null)
                    {
                        logger.LogDebug("Unknown sequel for {AnimeName} (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle, anime.ID, sequel.RelatedID);
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
                    logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName} (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle, anime.DefaultTitle, anime.ID, sequelSearch.ID);
                    var sequelResult = await GetReleaseInfoForMatchAndAnime(sequelMatch, sequelSearch, cancellationToken, depth + 1, year).ConfigureAwait(false);
                    if (sequelResult is not null)
                    {
                        logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName} (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle, anime.DefaultTitle, anime.ID, sequelSearch.ID);
                        return sequelResult;
                    }
                }

                logger.LogDebug("No matched sequels found for {AnimeName} (Anime={AnimeID})", anime.DefaultTitle, anime.ID);
                return null;
            }

            logger.LogDebug("No episodes found for {AnimeName} (Anime={AnimeID})", anime.DefaultTitle, anime.ID);
            return null;
        }

        if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
        {
            logger.LogDebug("Year mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.ShowName, anime.DefaultTitle, anime.ID, anime.AirDate?.Year, year);
            return null;
        }

        foreach (var episode in episodes)
        {
            logger.LogDebug("Found episode {EpisodeType} {EpisodeNumber} for {ShowName} (Anime={AnimeID})", episode.Type.ToString(), episode.EpisodeNumber, anime.DefaultTitle, anime.ID);
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
        if (!releaseId.StartsWith(IdPrefix))
        {
            logger.LogWarning("Invalid release id: {ReleaseId}", releaseId);
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

    [GeneratedRegex(@"^(?:(?<isFinalEpisode>Final Episode) |Episode (?<episodeRange1>\d+(?:\-\d+)?) |(?<isFull1>Full) |(?<isDVD>DVD) |(?<isSpecial1>Special|Bonus|Special Broadcast|TV)? |(?<isCreditless>creditless) |(?<isInternational>international) |(?<isOriginal>Original )?(?<language>japanese|english|american|us|german|french(?: uncensored)?|italian|spanish|arabic) |(?<isEdgeCase>BD-BOX II|Doukyuusei: Natsu no Owari ni|Ami-chan's First Love|Narration -) )?(?<type>ED|OP|Opening|Ending|Intro|Credits)(?<isExtra> EX)?(?: *(?<number>\d+))?(?<suffix> ?[a-z]|\.\d+)?(?:(?: \((?<episodeRange2>\d+(?:\-\d+)?)\)| Episode (?<episodeRange3>\d+(?:\-\d+)?))?| \((?:(?<isSpecial2>Specials?)|Episode (?<episodeRange4>S?\d+(?:\-\d+)?(?: ?& ?S?\d+(?:\-\d+)?)?)|(?<isFull2>LongVer.)|(?<details1>[^\)]+))\)| \[(?<details2>[^\]]+)\]| (?<isFull3>full)| - Version (?<version>\d+)| - .+| .+)?$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreditTitleRegex();

    [GeneratedRegex(@"\[anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\]|,),?)+\]|\(anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\)|,),?)+\)|\{anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*\d+\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFolderNameCheckRegex();

    [GeneratedRegex(@"\[anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\]|,),?)+\]|\(anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\)|,),?)+\)|\{anidb[\-= ](?:(?<=anidb-|anidb=|anidb |,)\s*(?:\d+[=\- \.])?\d+(?:'\d+(?:\-\d+)?)?\s*(?=\}|,),?)+\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StrictFilenameCheckRegex();

    [GeneratedRegex(@"(?<=^|,)\s*(?:(?<animeId>\d+)[=\- \.])?(?<episodeId>\d+)(?:'(?<percentRangeStartOrWholeRange>\d+)(?:\-(?<percentRangeEnd>\d+))?)?\s*(?=$|,),?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();

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
