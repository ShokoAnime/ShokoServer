using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Contains information about a parsed file path.
/// </summary>
public record ParsedFileResult
{
    /// <summary>
    /// Whether the file was matched.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// The file path.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The file extension.
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// The release group.
    /// </summary>
    public string? ReleaseGroup { get; set; }

    /// <summary>
    /// The series name.
    /// </summary>
    public string? SeriesName { get; set; }

    /// <summary>
    /// The series type.
    /// </summary>
    public AnimeType? SeriesType { get; set; }

    /// <summary>
    /// The show's release year, if found.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// The season.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// The episode name.
    /// </summary>
    public string? EpisodeName { get; set; }

    /// <summary>
    /// The episode start number.
    /// </summary>
    public float EpisodeStart { get; set; }

    /// <summary>
    /// The episode end number.
    /// </summary>
    public float EpisodeEnd { get; set; }

    /// <summary>
    /// The episode text, different from the episode name as it describes
    /// the episode range in textual form.
    /// </summary>
    public string? EpisodeText { get; set; }

    /// <summary>
    /// Indicates if the theme video is creditless.
    /// </summary>
    public bool? Creditless { get; set; }

    /// <summary>
    /// Indicates if the episode is censored or uncensored.
    /// </summary>
    public bool? Censored { get; set; }

    /// <summary>
    /// The episode type.
    /// </summary>
    public EpisodeType EpisodeType { get; set; }

    /// <summary>
    /// The release source.
    /// </summary>
    public ReleaseSource? Source { get; set; }

    /// <summary>
    /// The version.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// The rule name used to match this file.
    /// </summary>
    public required string RuleName { get; set; }

    /// <summary>
    /// An empty result.
    /// </summary>
    public static ParsedFileResult Empty => new()
    {
        Success = false,
        FilePath = string.Empty,
        RuleName = "none",
    };

    /// <summary>
    /// Attempts to match a file path to a rule.
    /// </summary>
    /// <param name="filePath">The file name to match.</param>
    /// <param name="rules">The rules to use.</param>
    /// <returns></returns>
    public static ParsedFileResult Match(string? filePath, IReadOnlyList<CompiledRule> rules)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Empty;

        var fileName = CleanFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return Empty;

        foreach (var rule in rules)
        {
            var match = rule.Regex.Match(rule.UsePath ? filePath : fileName);
            if (match.Success)
            {
                var episodeStart = 1f;
                var episodeEnd = 1f;
                // We accept specials in-between episodes or episode ranges, so we split
                // the range and parse the text as floats.
                var episodeParts = (match.Groups["specialNumber"].Success ? match.Groups["specialNumber"] : match.Groups["episode"]).Value.Split('-');
                if (episodeParts.Length > 0)
                {
                    var episodeStartStr = episodeParts[0].TrimStart('E');
                    if (float.TryParse(episodeStartStr, out var episodeStartFloat))
                    {
                        episodeStart = episodeStartFloat;
                    }
                    if (episodeParts.Length == 1)
                        episodeEnd = episodeStart;
                }
                if (episodeParts.Length > 1)
                {
                    var episodeEndStr = episodeParts[1].TrimStart('E');
                    if (float.TryParse(episodeEndStr, out var episodeEndFloat))
                    {
                        episodeEnd = episodeEndFloat;
                    }
                }

                // Swap episode numbers if they're reversed.
                if (episodeEnd - episodeStart < 0)
                    (episodeStart, episodeEnd) = (episodeEnd, episodeStart);

                var seriesType = DetectAnimeType(match.Groups);
                var episodeType = DetectEpisodeType(match.Groups);
                var episodeText = (string?)null;
                if (episodeType == EpisodeType.Episode && episodeStart == episodeEnd && !float.IsInteger(episodeStart))
                {
                    episodeType = EpisodeType.Special;
                    episodeText = episodeParts[0].TrimStart('E');
                    episodeStart = 0;
                    episodeEnd = 0;
                }

                var showName = match.Groups["showName"]?.Value?.Trim();
                if (showName == "Episode")
                    showName = null;
                var initialDetails = new ParsedFileResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileExtension = match.Groups["extension"]?.Value,
                    ReleaseGroup = match.Groups["releaseGroup"]?.Value,
                    SeriesName = showName,
                    SeriesType = seriesType,
                    Year = match.Groups["year"].Value is { Length: > 0 } ? int.Parse(match.Groups["year"].Value) : null,
                    SeasonNumber = match.Groups["season"].Value is { Length: > 0 } ? int.Parse(match.Groups["season"].Value) : null,
                    EpisodeName = match.Groups["episodeName"]?.Value,
                    EpisodeStart = episodeStart,
                    EpisodeEnd = episodeEnd,
                    EpisodeType = episodeType,
                    EpisodeText = episodeText,
                    Version = match.Groups["version"].Value is { Length: > 0 } ? int.Parse(match.Groups["version"].Value) : null,
                    RuleName = rule.Name,
                };

                var finalDetails = rule.Transform(initialDetails, match);
                if (finalDetails is { Success: true })
                    return finalDetails;

                // Break the loop if we receive `null`, continue the loop if we receive
                // `{ Success: false }`.
                if (finalDetails is null)
                    break;
            }
        }

        return Empty;
    }

    private static string? CleanFileName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        while (_bracketIdRegex.Match(fileName) is { Success: true } match)
        {
            fileName = fileName[..match.Index] + fileName[(match.Index + match.Length)..];
        }

        return fileName;
    }

    private static AnimeType? DetectAnimeType(GroupCollection matchGroups)
    {
        if (matchGroups["isMovie"].Success || matchGroups["isMovie2"].Success)
            return AnimeType.Movie;

        if (matchGroups["isOVA"].Success || matchGroups["isOVA2"].Success)
            return AnimeType.Movie;

        return null;
    }

    private static EpisodeType DetectEpisodeType(GroupCollection matchGroups)
    {
        if (matchGroups["isSpecial"].Success || matchGroups["specialNumber"].Success || (matchGroups["season"] is { Success: true, Value: "0" or "00" or "000" }))
        {
            return EpisodeType.Special;
        }

        if (matchGroups["isThemeSong"].Success)
        {
            return EpisodeType.Credits;
        }

        if (matchGroups["isOther"].Success)
        {
            return EpisodeType.Other;
        }

        if (matchGroups["isTrailer"].Success)
        {
            return EpisodeType.Trailer;
        }

        return EpisodeType.Episode;
    }

    /// <summary>
    /// Default transform.
    /// </summary>
    /// <param name="originalDetails"></param>
    /// <param name="match"></param>
    /// <returns></returns>
    public static ParsedFileResult? DefaultTransform(ParsedFileResult originalDetails, Match match)
    {
        var modifiedDetails = originalDetails with { };

        // Season 0 means it's a special.
        if (modifiedDetails is { SeasonNumber: 0, EpisodeType: EpisodeType.Episode })
            modifiedDetails.EpisodeType = EpisodeType.Special;

        // Fix up show name by removing unwanted details and fixing spaces.
        if (modifiedDetails.SeriesName is not null)
        {
            var showName = modifiedDetails.SeriesName
                .Replace(_miscRegex, string.Empty)
                .Replace(_bracketCollapseRegex, string.Empty)
                .Replace(_spaceCollapseRegex, " ")
                .Replace(_bracketTrimRegex, string.Empty);

            // Fix movie name when no episode number is provided.
            var episode = (match.Groups["specialNumber"].Success ? match.Groups["specialNumber"] : match.Groups["episode"]).Value;
            var episodeName = match.Groups["episodeName"].Value;
            var isTv = match.Groups["isTv"].Success;
            var year = match.Groups["year"].Value;
            if (!string.IsNullOrEmpty(episodeName) && string.IsNullOrEmpty(episode))
            {
                var rangeStart = match.Groups["showName"].Index + match.Groups["showName"].Length;
                var rangeEnd = match.Groups["episodeName"].Index;
                var inBetween = match.Value[rangeStart..rangeEnd];
                if (_reStitchRegex.IsMatch(inBetween))
                {
                    showName += inBetween + episodeName
                        .Replace(_miscRegex, string.Empty)
                        .Replace(_bracketCollapseRegex, string.Empty)
                        .Replace(_spaceCollapseRegex, " ")
                        .Replace(_bracketTrimRegex, string.Empty);
                    modifiedDetails.EpisodeName = null;
                }
            }

            // Convert underscores and dots to spaces if we don't have any spaces in
            // the show name yet.
            if (!showName.Contains(' '))
            {
                showName = showName.Replace("_", " ").Replace(".", " ").Trim();

                // A hack.
                if (showName.StartsWith("hack//"))
                    showName = $".{showName}";
            }

            var matchResult = showName.Match(@" S(\d+)$");
            if (matchResult.Success)
            {
                showName = showName[..^matchResult.Length];
                modifiedDetails.SeasonNumber = int.Parse(matchResult.Groups[1].Value);
            }

            // Fix up year for some shows.
            matchResult = showName.Match(@" (?:19|2[01])\d{2}$");
            if (matchResult.Success)
            {
                modifiedDetails.Year = int.Parse(matchResult.Value.Trim());
                showName = $"{showName[..^matchResult.Length]} ({matchResult.Value.Trim()})";
            }

            // Append back the '(TV)' part if we removed it.
            if (isTv && !showName.EndsWith(" (TV)"))
                showName += " (TV)";
            else if (showName.EndsWith(" - TV"))
                showName = $"{showName[..^5]} (TV)";
            else if (showName.EndsWith(" TV"))
                showName = $"{showName[..^3]} (TV)";

            // Append the 'season' number to the show name to help with the search if a
            // year was not found.
            if (modifiedDetails.SeasonNumber is > 1 && !string.IsNullOrEmpty(episode) && string.IsNullOrEmpty(year))
                showName += $" S{modifiedDetails.SeasonNumber}";

            modifiedDetails.SeriesName = showName;
        }

        if (modifiedDetails.EpisodeName != null)
        {
            var episodeName = modifiedDetails.EpisodeName
                .Replace(_miscRegex, string.Empty)
                .Replace(_bracketCollapseRegex, string.Empty)
                .Replace(_spaceCollapseRegex, " ")
                .Replace(_bracketTrimRegex, string.Empty);

            // Convert underscores and dots to spaces if we don't have any spaces in
            // the show name yet.
            if (!episodeName.Contains(' '))
                episodeName = episodeName.Replace("_", " ").Replace(".", " ").Trim();

            modifiedDetails.EpisodeName = episodeName;
            if (string.IsNullOrEmpty(episodeName))
                modifiedDetails.EpisodeName = null;
        }

        if (modifiedDetails.Source is null && _sourceRegex.Match(originalDetails.FilePath) is { Success: true } sourceResult)
        {
            modifiedDetails.Source = sourceResult.Groups["source"].Value.ToUpper() switch
            {
                "BD" => ReleaseSource.BluRay,
                "BluRay" => ReleaseSource.BluRay,
                "BlueRay" => ReleaseSource.BluRay,
                "Blu-Ray" => ReleaseSource.BluRay,
                "Blue-Ray" => ReleaseSource.BluRay,
                "HDBD" => ReleaseSource.BluRay,
                "HD BD" => ReleaseSource.BluRay,
                "UHDBD" => ReleaseSource.BluRay,
                "UHD BD" => ReleaseSource.BluRay,
                "DVD" => ReleaseSource.DVD,
                "HDDVD" => ReleaseSource.DVD,
                "HD DVD" => ReleaseSource.DVD,
                "HKDVD" => ReleaseSource.DVD,
                "HK DVD" => ReleaseSource.DVD,
                "VHS" => ReleaseSource.VHS,
                "VCD" => ReleaseSource.VCD,
                "SVCD" => ReleaseSource.VCD,
                "CAMERA" => ReleaseSource.Camera,
                "CAMCORDER" => ReleaseSource.Camera,
                "LD" => ReleaseSource.LaserDisc,
                "LASERDISC" => ReleaseSource.LaserDisc,
                "TV" => ReleaseSource.TV,
                "SDTV" => ReleaseSource.TV,
                "SD TV" => ReleaseSource.TV,
                "HDTV" => ReleaseSource.TV,
                "HD TV" => ReleaseSource.TV,
                "UHDTV" => ReleaseSource.TV,
                "UHD TV" => ReleaseSource.TV,
                "WEB" => ReleaseSource.Web,
                "WWW" => ReleaseSource.Web,
                _ => ReleaseSource.Unknown,
            };
        }

        if (modifiedDetails.Censored is null && _censoredRegex.Match(originalDetails.FilePath) is { Success: true } censoredResult)
            modifiedDetails.Censored = !censoredResult.Groups["isDe"].Success;

        if (modifiedDetails.EpisodeStart == 0)
        {
            var trailerCheckResult = _trailerCheckRegex.Match(originalDetails.FilePath);
            if (trailerCheckResult.Success)
            {
                var episodeText = trailerCheckResult.Groups["episode"].Value ?? "0";
                var episode = int.Parse(episodeText);

                modifiedDetails.EpisodeType = EpisodeType.Trailer;
                if (episode > 0)
                {
                    modifiedDetails.EpisodeStart = episode;
                    modifiedDetails.EpisodeEnd = episode;
                }
            }
            var extraCheckResult = _extraCheckRegex.Match(originalDetails.FilePath);
            if (extraCheckResult.Success && modifiedDetails.EpisodeStart == 0)
                return null;

            // Special handling of episode 0.
            if (modifiedDetails is { EpisodeType: EpisodeType.Episode, SeasonNumber: null, EpisodeName: null or "", EpisodeText: null or "", EpisodeStart: 0, EpisodeEnd: 0 })
            {
                modifiedDetails.EpisodeType = EpisodeType.Special;
                modifiedDetails.EpisodeStart = 1;
                modifiedDetails.EpisodeEnd = 1;
            }
        }

        // Correct movie numbering.
        if (
            (match.Groups["isMovie"].Success || match.Groups["isMovie2"].Success) && modifiedDetails.EpisodeStart == 0
            && modifiedDetails.EpisodeEnd == 0
        )
        {
            modifiedDetails.EpisodeStart = 1;
            modifiedDetails.EpisodeEnd = 1;
        }

        // Basic OVA/movie detection.
        if (modifiedDetails is { SeriesType: null })
        {
            if (modifiedDetails.FilePath.Match(@"(?<=\b|_)[Tt]he[\._ ][Mm]ovie|[Gg]ekijoban(?=\b|_)") is { Success: true })
                modifiedDetails.SeriesType = AnimeType.Movie;
            else if (modifiedDetails.FilePath.Match(@"(?<=\b|_)(?:OVA|OAD)(?=\b|_|v\d+)") is { Success: true })
                modifiedDetails.SeriesType = AnimeType.OVA;
        }

        return modifiedDetails;
    }

    /// <summary>
    /// Pre/post transform.
    /// </summary>
    /// <param name="originalDetails"></param>
    /// <param name="match"></param>
    /// <returns></returns>
    public static ParsedFileResult? PrePostTransform(ParsedFileResult originalDetails, Match match)
    {
        var modifiedDetails = originalDetails with { };
        if (match.Groups["pre"].Value is { Length: > 0 } pre)
        {
            if (pre.Length > 0 && pre[^1] is '(' or '[' or '{')
                pre = pre[0..^1];
            if (_leadingReleaseGroupCheck.Match(pre) is { Success: true } releaseGroupMatch)
            {
                modifiedDetails.ReleaseGroup = releaseGroupMatch.Groups["releaseGroup"].Value[1..^1];
                pre = pre[releaseGroupMatch.Length..];
            }
            if (!pre.Contains(' '))
            {
                pre = pre.Replace("_", " ").Replace(".", " ").Trim();

                // A hack.
                if (pre.StartsWith("hack//"))
                    pre = $".{pre}";
            }
            if (pre.Match(@" \((?<year>\d{4})\)") is { Success: true } yearMatch)
                modifiedDetails.Year = int.Parse(yearMatch.Groups["year"].Value);
            modifiedDetails.SeriesName = pre.Trim();
        }
        if (match.Groups["post"].Value is { Length: > 0 } post)
        {
            if (post.Length > 0 && post[0] is ')' or ']' or '}')
                post = post[1..];
            if (_sourceRegex.Match(post) is { Success: true } sourceResult)
            {
                modifiedDetails.Source = sourceResult.Groups["source"].Value.ToUpper() switch
                {
                    "BD" => ReleaseSource.BluRay,
                    "BluRay" => ReleaseSource.BluRay,
                    "BlueRay" => ReleaseSource.BluRay,
                    "Blu-Ray" => ReleaseSource.BluRay,
                    "Blue-Ray" => ReleaseSource.BluRay,
                    "HDBD" => ReleaseSource.BluRay,
                    "HD BD" => ReleaseSource.BluRay,
                    "UHDBD" => ReleaseSource.BluRay,
                    "UHD BD" => ReleaseSource.BluRay,
                    "DVD" => ReleaseSource.DVD,
                    "HDDVD" => ReleaseSource.DVD,
                    "HD DVD" => ReleaseSource.DVD,
                    "HKDVD" => ReleaseSource.DVD,
                    "HK DVD" => ReleaseSource.DVD,
                    "VHS" => ReleaseSource.VHS,
                    "VCD" => ReleaseSource.VCD,
                    "SVCD" => ReleaseSource.VCD,
                    "CAMERA" => ReleaseSource.Camera,
                    "CAMCORDER" => ReleaseSource.Camera,
                    "LD" => ReleaseSource.LaserDisc,
                    "LASERDISC" => ReleaseSource.LaserDisc,
                    "TV" => ReleaseSource.TV,
                    "SDTV" => ReleaseSource.TV,
                    "SD TV" => ReleaseSource.TV,
                    "HDTV" => ReleaseSource.TV,
                    "HD TV" => ReleaseSource.TV,
                    "UHDTV" => ReleaseSource.TV,
                    "UHD TV" => ReleaseSource.TV,
                    "WEB" => ReleaseSource.Web,
                    "WEBDL" => ReleaseSource.Web,
                    "WWW" => ReleaseSource.Web,
                    _ => ReleaseSource.Unknown,
                };
            }
            post = post.Replace(_miscRegex, string.Empty).Replace(_bracketCollapseRegex, string.Empty);
            if (_trailingReleaseGroupCheck.Match(post) is { Success: true } releaseGroupMatch)
            {
                if (string.IsNullOrEmpty(modifiedDetails.ReleaseGroup))
                    modifiedDetails.ReleaseGroup = releaseGroupMatch.Groups["releaseGroup"].Value;
                post = post[..^releaseGroupMatch.Length];
            }
            post = post.Replace(_spaceCollapseRegex, " ").Replace(_bracketTrimRegex, string.Empty);
            if (post.Length > 2 && ((post[0] == '(' && post[^1] == ')') || (post[0] == '[' && post[^1] == ']') || (post[0] == '{' && post[^1] == '}') || (post[0] == '「' && post[^1] == '」')))
                post = post[1..^1];

            if (!string.IsNullOrEmpty(post))
                modifiedDetails.EpisodeName = post.Trim();
        }
        if (match.Groups["isThemeSong"].Success)
        {
            var episodeText = match.Groups["episode"].Value;
            if (string.IsNullOrEmpty(episodeText))
                episodeText = "1";

            var episode = int.Parse(episodeText);
            var episodeTextDetails = $"{match.Groups["isThemeSong"].Value}{(match.Groups["episode"].Success ? episode.ToString() : "")}{match.Groups["themeSuffix"].Value}";
            modifiedDetails.Creditless = match.Groups["isCreditless"].Success;
            modifiedDetails.EpisodeType = EpisodeType.Credits;
            modifiedDetails.EpisodeStart = episode;
            modifiedDetails.EpisodeEnd = episode;
            modifiedDetails.EpisodeText = episodeTextDetails;
        }
        return DefaultTransform(modifiedDetails, match);
    }

    /// <summary>
    /// Fallback transform.
    /// </summary>
    /// <param name="originalDetails"></param>
    /// <param name="match"></param>
    /// <returns></returns>
    public static ParsedFileResult? FallbackTransform(ParsedFileResult originalDetails, Match match)
    {
        var modifiedDetails = originalDetails with { };
        if (match.Groups["fallback"].Value is { Length: > 0 } fallback)
        {
            if (_leadingReleaseGroupCheck.Match(fallback) is { Success: true } releaseGroupMatch)
            {
                modifiedDetails.ReleaseGroup = releaseGroupMatch.Groups["releaseGroup"].Value[1..^1];
                fallback = fallback[releaseGroupMatch.Length..];
            }
            if (_sourceRegex.Match(fallback) is { Success: true } sourceResult)
            {
                modifiedDetails.Source = sourceResult.Groups["source"].Value.ToUpper() switch
                {
                    "BD" => ReleaseSource.BluRay,
                    "BluRay" => ReleaseSource.BluRay,
                    "BlueRay" => ReleaseSource.BluRay,
                    "Blu-Ray" => ReleaseSource.BluRay,
                    "Blue-Ray" => ReleaseSource.BluRay,
                    "HDBD" => ReleaseSource.BluRay,
                    "HD BD" => ReleaseSource.BluRay,
                    "UHDBD" => ReleaseSource.BluRay,
                    "UHD BD" => ReleaseSource.BluRay,
                    "DVD" => ReleaseSource.DVD,
                    "HDDVD" => ReleaseSource.DVD,
                    "HD DVD" => ReleaseSource.DVD,
                    "HKDVD" => ReleaseSource.DVD,
                    "HK DVD" => ReleaseSource.DVD,
                    "VHS" => ReleaseSource.VHS,
                    "VCD" => ReleaseSource.VCD,
                    "SVCD" => ReleaseSource.VCD,
                    "CAMERA" => ReleaseSource.Camera,
                    "CAMCORDER" => ReleaseSource.Camera,
                    "LD" => ReleaseSource.LaserDisc,
                    "LASERDISC" => ReleaseSource.LaserDisc,
                    "TV" => ReleaseSource.TV,
                    "SDTV" => ReleaseSource.TV,
                    "SD TV" => ReleaseSource.TV,
                    "HDTV" => ReleaseSource.TV,
                    "HD TV" => ReleaseSource.TV,
                    "UHDTV" => ReleaseSource.TV,
                    "UHD TV" => ReleaseSource.TV,
                    "WEB" => ReleaseSource.Web,
                    "WEBDL" => ReleaseSource.Web,
                    "WWW" => ReleaseSource.Web,
                    _ => ReleaseSource.Unknown,
                };
            }
            fallback = fallback.Replace(_miscRegex, string.Empty).Replace(_bracketCollapseRegex, string.Empty);
            if (_trailingReleaseGroupCheck.Match(fallback) is { Success: true } releaseGroupMatch1)
            {
                modifiedDetails.ReleaseGroup = releaseGroupMatch1.Groups["releaseGroup"].Value;
                fallback = fallback[..^releaseGroupMatch1.Length];
            }
            fallback = fallback.Replace(_spaceCollapseRegex, " ").Replace(_bracketTrimRegex, string.Empty);
            if (fallback.Length > 2 && ((fallback[0] == '(' && fallback[^1] == ')') || (fallback[0] == '[' && fallback[^1] == ']') || (fallback[0] == '{' && fallback[^1] == '}') || (fallback[0] == '「' && fallback[^1] == '」')))
                fallback = fallback[1..^1];
            if (!fallback.Contains(' '))
            {
                fallback = fallback.Replace("_", " ").Replace(".", " ").Trim();

                // A hack.
                if (fallback.StartsWith("hack//"))
                    fallback = $".{fallback}";
            }
            if (fallback.Match(@" \((?<year>\d{4})\)") is { Success: true } yearMatch)
                modifiedDetails.Year = int.Parse(yearMatch.Groups["year"].Value);
            modifiedDetails.SeriesName = fallback.Trim();
        }
        return DefaultTransform(modifiedDetails, match);
    }

    /// <summary>
    /// Invalidates the match.
    /// </summary>
    /// <param name="originalDetails"></param>
    /// <param name="match"></param>
    /// <returns>Always null.</returns>
    public static ParsedFileResult? DenyTransform(ParsedFileResult originalDetails, Match match) => null;

    private static readonly Regex _bracketIdRegex = new(
        @"[\[\(\{](?<provider>(?<providerName>anidb|anilist|mal|kitsu|tmdb|moviedb|tvdb|imdb)(?<providerMode>[1-4])?(?<divider>[\-= ]))(?<value>(?:(?<=\k<provider>|,)\s*(?:[^\]\)\}\,\s]+)\s*(?=[\]\)\}]|,),?)+)[\]\)\}]",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _leadingReleaseGroupCheck = new(
        @"^(?<releaseGroup>\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\})",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _trailingReleaseGroupCheck = new(
        @"[\. _]+-(?<releaseGroup>\w+)(?: \([^\)]+\))?[\. _]*$|[\. _]*\[(?<releaseGroup>[\w \.]+)\][\. _]*$",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _reStitchRegex = new(@"^[\s_.]*-+[\s_.]*$|^[\s_.]*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _trailerCheckRegex = new(
        @"(?<isTrailer>(?<![a-z0-9])(?:(?:character|web)[\s_.]*)?(?:cm|pv|trailer)(?![a-z]))(?:[\s_.]*(?<episode>\d+(?!\d*p)))?",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _extraCheckRegex = new(
        @"(?<isTrailer>(?<![a-z0-9])(?:(?:bd)[\s_.]*)?(?:menu)(?![a-z]))(?:[\s_.]*(?<episode>\d+(?!\d*p)))?",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _sourceRegex = new(
        @"\b(?<source>(?:U?HD[ _-]?|SD[ _-]?)?TV(?!-cm)|(?:U?HD[ _-]?)?(?:BD|Blue?-?Ray)(?! (?:menu|notice))|(?:H[KD] ?)?DVD|VHS|S?VCD|Web(?:-?DL)?|www|(?:\b|(?<=_))LD(?:\b|(?=_))|LaserDisc|camera|camcorder)s?(?:[ _-]?rip)?\b",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _miscRegex = new(
        @"(?:(?<source>(?:U?HD[ _-]?|SD[ _-]?)?TV(?!-cm)|(?:U?HD[ _-]?)?(?:BD|Blue?-?Ray)(?! (?:menu|notice))|(?:H[KD] ?)?DVD|VHS|S?VCD|Web(?:-?DL)?|www|(?:\b|(?<=_))LD(?:\b|(?=_))|LaserDisc|camera|camcorder)s?(?:[ _-]?rip)?|(?<lang>(?:\b|(?<=_))lng(?:\b|(?=_))|(?:\b|(?<=_))jpn?(?:\b|(?=_))|jap(?:anese)?|(?:\b|(?<=_))gb(?:\b|(?=_))|eng(?:lish)?|(?:\b|(?<=_))en(?:\b|(?=_))|(?:\b|(?<=_))es\b|(?:\b|(?<=_))cn(?:\b|(?=_))|chinese|spa(?:nish)?|(?:\b|(?<=_))de(?:\b|(?=_))|ger(?:man)?)|(?<codec>(?:xvid|divx|prores|vvc|hevc|avc|mpeg[\.-]?[1-4]|vc1|av1|flv|[hx]\.?26[1-6]|aac|e?ac-?3|flac|dca|ogg|opus|wmav2|wmapro|adpcm_ms|pcm|mp[23]|vp[69]f?)(?:[ \._-]?[1-9]\.[0-9](?:\.[0-9])?|-(?:8|10)bits?)?)|(?:multi(?:(?:ple)? )?)?sub(?:s|titled?)?|dub(?:bed)?|rip|(un)?cen(ored)?|(?<resolution>[48]k|\d{3,5}[pi]|\d{3,5}[x×]\d{3,5})|multi(?:[-_ ]?(?:pack|audio))?|remux|truehd|hi10[pi]?|(?:\b|(?<=_))proper(?:\b|(?=_))|dolby (?:atmos|vision)?|\bdovi\b|dts|vostfr|vorbis|crf\d+|at-x|dual[-_ ]?audio|[24579]ch|(?:8|10)-?bits?|[0-9a-f]{8})",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _bracketCollapseRegex = new(
        @"\[[ \._\-]*\]|\([ \._\-]*\)|\{[ \._\-]*\}",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _spaceCollapseRegex = new(
        @"\s{2,}",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _bracketTrimRegex = new(
        @"^[ \)\]\}]+|[ \(\[\{]+$",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _censoredRegex = new(
        @"\b((?<isDe>de|un)?cen(?:sored)?)\b",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// A compiled rule with a ready-to-use regex and a transform function.
    /// </summary>
    public class CompiledRule
    {
        /// <summary>
        /// The name of the rule.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Indicates that the regex should be applied to the file path instead of the file name.
        /// </summary>
        public bool UsePath { get; init; }

        /// <summary>
        /// The regex pattern to match.
        /// </summary>
        public required Regex Regex { get; init; }

        /// <summary>
        /// The transform function.
        /// </summary>
        public Func<ParsedFileResult, Match, ParsedFileResult?> Transform { get; init; } = DefaultTransform;
    }
}
