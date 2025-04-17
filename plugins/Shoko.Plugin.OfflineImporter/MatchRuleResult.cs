using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Contains information about a parsed file path.
/// </summary>
public record MatchRuleResult
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
    /// The show name.
    /// </summary>
    public string? ShowName { get; set; }

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
    /// The episode text.
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
    public static MatchRuleResult Empty => new()
    {
        Success = false,
        FilePath = string.Empty,
        RuleName = "none",
    };

    /// <summary>
    /// Attempts to match a file path to a rule.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static MatchRuleResult Match(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Empty;

        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return Empty;

        foreach (var rule in _rules)
        {
            var match = rule.Regex.Match(fileName);
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

                var episodeType = DetectEpisodeType(match.Groups);
                if (episodeType == EpisodeType.Episode && episodeStart == episodeEnd && !float.IsInteger(episodeStart))
                {
                    episodeType = EpisodeType.Special;
                    episodeStart = 0;
                    episodeEnd = 0;
                }

                var showName = match.Groups["showName"]?.Value?.Trim();
                if (showName == "Episode")
                    showName = null;
                var initialDetails = new MatchRuleResult
                {
                    Success = true,
                    FilePath = filePath,
                    FileExtension = match.Groups["extension"]?.Value,
                    ReleaseGroup = match.Groups["releaseGroup"]?.Value,
                    ShowName = showName,
                    Year = match.Groups["year"].Value is { Length: > 0 } ? int.Parse(match.Groups["year"].Value) : null,
                    SeasonNumber = match.Groups["season"].Value is { Length: > 0 } ? int.Parse(match.Groups["season"].Value) : null,
                    EpisodeName = match.Groups["episodeName"]?.Value,
                    EpisodeStart = episodeStart,
                    EpisodeEnd = episodeEnd,
                    EpisodeType = episodeType,
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

    private static MatchRuleResult? DefaultTransform(MatchRuleResult originalDetails, Match match)
    {
        var modifiedDetails = originalDetails with { };

        // Season 0 means it's a special.
        if (modifiedDetails is { SeasonNumber: 0, EpisodeType: EpisodeType.Episode })
            modifiedDetails.EpisodeType = EpisodeType.Special;

        // Fix up show name by removing unwanted details and fixing spaces.
        if (modifiedDetails.ShowName is not null)
        {
            var showName = modifiedDetails.ShowName.Trim().Replace(_trimShowNameRegex, string.Empty);

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
                    showName += inBetween + episodeName.Replace(_trimShowNameRegex, "");
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

            modifiedDetails.ShowName = showName;
        }

        if (modifiedDetails.EpisodeName != null)
        {
            var episodeName = modifiedDetails.EpisodeName.Replace(_trimShowNameRegex, "");

            // Convert underscores and dots to spaces if we don't have any spaces in
            // the show name yet.
            if (!episodeName.Contains(' '))
                episodeName = episodeName.Replace("_", " ").Replace(".", " ").Trim();

            modifiedDetails.EpisodeName = episodeName;
        }

        var sourceResult = _sourceRegex.Match(originalDetails.FilePath);
        if (sourceResult.Success)
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

        var censoredResult = _censoredRegex.Match(originalDetails.FilePath);
        if (censoredResult.Success)
            modifiedDetails.Censored = !censoredResult.Groups["isDe"].Success;

        // Add theme video information if found.
        var themeCheckResult = _themeSongCheckRegex.Match(originalDetails.FilePath);
        if (themeCheckResult.Success)
        {
            var episodeText = themeCheckResult.Groups["episode"].Value;
            if (string.IsNullOrEmpty(episodeText))
                episodeText = "1";

            var episode = int.Parse(episodeText);
            var episodeTextDetails = $"{themeCheckResult.Groups["type"].Value}{(themeCheckResult.Groups["episode"].Success ? episode.ToString() : "")}{themeCheckResult.Groups["suffix"].Value}";
            modifiedDetails.Creditless = themeCheckResult.Groups["isCreditless"].Success;
            modifiedDetails.EpisodeType = EpisodeType.Credits;
            modifiedDetails.EpisodeStart = episode;
            modifiedDetails.EpisodeEnd = episode;
            modifiedDetails.EpisodeText = episodeTextDetails;
        }

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
            {
                var episodeText = extraCheckResult.Groups["episode"].Value ?? "0";
                var episode = int.Parse(episodeText);

                modifiedDetails.EpisodeType = EpisodeType.Other;
                if (episode > 0)
                {
                    modifiedDetails.EpisodeStart = episode;
                    modifiedDetails.EpisodeEnd = episode;
                }
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
        return modifiedDetails;
    }

    private static readonly Regex _leadingReleaseGroupCheck = new(
        @"^(?<releaseGroup>\[[^\]]+\]|\([^\)]+\)|\{[^\}]+\})",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _trailingReleaseGroupCheck = new(
        @"(?<=[\. _])-(?<releaseGroup>\w+)(?: \([^\)]+\))?[\. _]*$",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _trimShowNameRegex = new(
        @"(?![\s_.]*\(part[\s_.]*[ivx]+\))(?![\s_.]*\((?:19|20)\d{2}\))(?:[\s_.]*(?:[([{][^)\]}\n]*[)\]}]|(?:(?<![a-z])(?:jpn?|jap(?:anese)?|en|eng(?:lish)?|es|(?:spa(?:nish)?|de|ger(?:man)?)|\d{3,4}[pi](?:-+hi\w*)?|(?:[uf]?hd|sd)|\d{3,4}x\d{3,4}|dual[\s_.-]*audio|(?:www|web|bd|dvd|ld|blu[\s_.-]*ray)(?:[\s_.-]*(?:rip|dl))?|dl|rip|(?:av1|hevc|[hx]26[45])(?:-[a-z0-9]{1,6})?|(?:dolby(?:[\s_.-]*atmos)?|dts|opus|ac3|aac|flac)(?:[\s._]*[257]\.[0124](?:[_.-]+\w{1,3})?)?|(?:\w{2,3}[\s_.-]*)?(?:sub(?:title)?s?|dub)|(?:un)?cen(?:\.|sored)?)[\s_.]*){1,20})){0,20}[\s_.]*$",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _reStitchRegex = new(@"^[\s_.]*-+[\s_.]*$|^[\s_.]*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _themeSongCheckRegex = new(
        @"(?<![a-z0-9])(?:(?<isCreditless>nc|[Cc]reditless|NC)[\s_.]*)?(?<type>ED|OP)(?![a-z]{2,})(?:[\s_.]*(?<episode>\d+(?!\d*p)))?(?<suffix>(?<=(?:OP|ED)(?:[\s_.]*\d+)?)(?:\.\d+|\w)\b)?",
        RegexOptions.ECMAScript | RegexOptions.Compiled
    );

    private static readonly Regex _trailerCheckRegex = new(
        @"(?<isTrailer>(?<![a-z0-9])(?:(?:character)[\s_.]*)?(?:cm|pv|trailer)(?![a-z]))(?:[\s_.]*(?<episode>\d+(?!\d*p)))?",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _extraCheckRegex = new(
        @"(?<isTrailer>(?<![a-z0-9])(?:(?:bd)[\s_.]*)?(?:menu|web preview)(?![a-z]))(?:[\s_.]*(?<episode>\d+(?!\d*p)))?",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _sourceRegex = new(
        @"\b(?<source>(?:U?HD[ _-]?|SD[ _-]?)?TV(?!-cm)|(?:U?HD[ _-]?)?(?:BD|Blue?-?Ray)(?! (?:menu|notice))|(?:H[KD] ?)?DVD|VHS|S?VCD|Web(?:-?DL)?|www|(?:\b|(?<=_))LD(?:\b|(?=_))|LaserDisc|camera|camcorder)s?(?:[ _-]?rip)?\b",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _miscRegex = new(
        @"(?:(?<source>(?:U?HD[ _-]?|SD[ _-]?)?TV(?!-cm)|(?:U?HD[ _-]?)?(?:BD|Blue?-?Ray)(?! (?:menu|notice))|(?:H[KD] ?)?DVD|VHS|S?VCD|Web(?:-?DL)?|www|(?:\b|(?<=_))LD(?:\b|(?=_))|LaserDisc|camera|camcorder)s?(?:[ _-]?rip)?|(?<lang>(?:\b|(?<=_))lng(?:\b|(?=_))|(?:\b|(?<=_))jpn?(?:\b|(?=_))|jap(?:anese)?|(?:\b|(?<=_))gb(?:\b|(?=_))|eng(?:lish)?|(?:\b|(?<=_))en(?:\b|(?=_))|(?:\b|(?<=_))es\b|(?:\b|(?<=_))cn(?:\b|(?=_))|chinese|spa(?:nish)?|(?:\b|(?<=_))de(?:\b|(?=_))|ger(?:man)?)|(?<codec>(?:xvid|divx|prores|vvc|hevc|avc|mpeg[\.-]?[1-4]|vc1|av1|flv|[hx]\.?26[1-6]|aac|e?ac-?3|flac|dca|ogg|opus|wmav2|wmapro|adpcm_ms|pcm|mp[23]|vp[69]f?)(?:[ \._-]?[1-9]\.[0-9](?:\.[0-9])?|-(?:8|10)bits?)?)|(?:multi(?:(?:ple)? )?)?sub(?:s|titled?)?|dub(?:bed)?|rip|(un)?cen(ored)?|(?<resolution>[48]k|\d{3,5}[pi]|\d{3,5}[x×]\d{3,5})|multi(?:[-_ ]?(?:pack|audio))?|remux|truehd|hi10|(?:\b|(?<=_))proper(?:\b|(?=_))|dolby (?:atmos|vision)?|\bdovi\b|dts|vostfr|vorbis|crf\d+|at-x|dual[-_ ]?audio|[24579]ch|(?:8|10)-?bits?|[0-9a-f]{8})",
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

    private static readonly Regex _censoredRegex = new(
        @"\b((?<isDe>de|un)?cen(?:sored)?)\b",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly MatchRule[] _rules = [
        new()
        {
            Name = "anti-timestamp",
            Regex = new(
                @"^\d{4}[._:\- ]\d{2}[._:\- ]\d{2}[._:\- T]\d{2}[._:\- ]\d{2}[._:\- ]\d{2}(?:[._:\- ]\d{1,6})?(?:Z|[+-]\d{2}:?\d{2})?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
            // invalidate the match.
            Transform = static (_, _) => null,
        },
        new()
        {
            Name = "trash-anime",
            Regex = new(
                @"^(?<showName>.+?(?: \((?<year>\d{4})\))) - (?:(?<isSpecial>S00?)|S\d+)E\d+(?:-E?\d+)? - (?<episode>\d+(?:-\d+)?) - (?<episodeName>.+?(?=\[)).*?(?:-(?<releaseGroup>[^\[\] ]+))?\s*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "new-default",
            Regex = new(
                @"^(?<pre>.*?(?<!(?:PV|CM|Preview|Trailer|Commercial|Menu|Jump Festa Special)[\._ ]?(-[\._ ]?)?))(?:[\._ ]?-[\._ ]?)?(?<=\b|_)(?:(?:s(?<season>\d+))?(?:OVA[\._ ]?|Vol(?:\.|ume)[\._ ]?|(?:(?:creditless|NC)[\._ ]?)?(?<isThemeSong>OP|ED|opening|ending)[\._ ]?(?!(?:10|2[01])\d{2})|(?<=\b|_)(?<isOther>o)|e(?:p(?:isode|s)?)?[\._ ]?|(?=(?:\d+(?:(?!-?\d+[pi])-\d+?|\.5|(?<=(?:ED|OP)[\. _]?\d+)[a-z])?)(?:v\d{1,2})?(?:[\._ ]?END(?=\b|_))?[\._ ]?\.[a-z0-9]{1,10}$)|(?<=[\._ ]?-[\._ ]?)(?=(?:\d+(?:(?!-\d+[pi])-\d+?|\.5|(?<=(?:ED|OP|opening|ending)[\._ ]?\d+)[a-z])?)(?:[\._ ]?END(?=\b|_))?[\._ ]?(?:[\._ ]?-[\._ ]?|[\[\({「]))|(?<=[\._ ]?-[\._ ]?)(?=(?:(?!(?:19|2[01])\d{2})\d+(?:(?!-\d+[pi])-\d+?|\.5|(?<=(?:ED|OP)[\. _]?\d+)[a-z])?)(?![\._ ]?-[\._ ]?))|(?=(?:(?!\d+[\. _]\(\d{4}\))\d+(?:(?!-\d+[pi])-\d+?|\.5|(?<=(?:ED|OP|opening|ending)[\. _]?\d+)[a-z])?)(?:v\d{1,2})?(?:[\._ ]?END(?=\b|_))?[\._ ]?[\[\({「])|(?<=s\d+) )(?<episode>\d+(?:(?!-\d+[pi])-\d+?|\.5|(?<=(?:ED|OP|opening|ending)[\._ ]?\d+)[a-z])?)(?!\]|\)|-(?!\d+[pi])\d+|[\._ ]?OVA| (?:nc)?(?:ed|op))|(?<!jump[\. _]festa[\. _])s(?:p(?:ecials?)?)?[ \.]?(?<specialNumber>\d+(?!\]|\)| \d+|[\. _]?-[\. _]?E(?:p(?:isode)?)?[ \.]?\d+))(?![\. _-]*(?:nc)?(?:ed|op)))(?:v(?<version>\d{1,2}))?(?=\b|_)(?:[\._ ]?END)?(?:[\._ ]?-[\._ ]?)?(?<post>.+)?\.(?<extension>[a-z0-9]{1,10})$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
            Transform = static (originalDetails, match) =>
            {
                var modifiedDetails = originalDetails with { };
                if (match.Groups["pre"].Value is { Length: > 0 } pre)
                {
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
                    modifiedDetails.ShowName = pre.Trim();
                }
                if (match.Groups["post"].Value is { Length: > 0 } post)
                {
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
                        modifiedDetails.ReleaseGroup = releaseGroupMatch.Groups["releaseGroup"].Value;
                        post = post[..^releaseGroupMatch.Length];
                    }
                    post = post.Replace(_spaceCollapseRegex, " ").Trim();
                    if (post.Length > 2 && ((post[0] == '(' && post[^1] == ')') || (post[0] == '[' && post[^1] == ']') || (post[0] == '{' && post[^1] == '}') || (post[0] == '「' && post[^1] == '」')))
                        post = post[1..^1];

                    if (!string.IsNullOrEmpty(post))
                        modifiedDetails.EpisodeName = post.Trim();
                }
                return DefaultTransform(modifiedDetails, match);
            },
        },
        new()
        {
            Name = "raws-1",
            Regex = new(
                @"^(?<showName>[^\n]+?) (?:(?:- ?)?(?:S(?<season>\d+)E|E?)(?<episode>\d+)(?:v(?<version>\d+))?(?: ?-)? )?(?:\w* )?(?<resolution>((?:[0-9]{3,4})x(?:[0-9]{3,4}))|(?:[0-9]{3,4})p)(?: [^ \n]+)*? ?(?<!DTS|Atmos|Dolby)-(?<releaseGroup>[^ \n]+)(?: \((?<source>[^)]+)\))?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "raws-2",
            Regex = new(
                @"^(?<showName>[^\n]+?)\.(?:-\.)?(?:S(?<season>\d+)E|E(?:p(?:isode)?\.?)?|(?<=(?<!\d)\.))(?<episode>(?<!\b[xh]\.?)\d+(?!\.(?:0|S\d+|E(?:p(?:isode)?\.?)?\d+)))(?:\.-)?(?:\.[^\.\n]+)*?-(?<releaseGroup>[^\.\n]+)(?:\.\((?<source>[^)]+)\))?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "raws-3",
            Regex = new(
                @"^(?<showName>[^\n]+?)(?:(?:- )?(?:S(?<season>\d+)E|E?)(?<episode>\d+)(?:v(?<version>\d+))?(?: -)? )?(?:[ \.](?:\[[^\]]+\]|{[^}]+})*)*-(?<releaseGroup>[A-Za-z0-9_]+)(?:\.\((?<source>[^)]+)\))?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "old-default",
            Regex = new(
                // Note: Currently it will not recognize episodes in the 19xx and 2xxx ranges. Let's hope nothing reaches that far.
                @"^(?:[{[(](?<releaseGroup>[^)}\]]+)[)}\]][\s_.]*)?(?<showName>(?<isMovie2>gekijouban[\s_.]+)?(?:[a-z]+[\s_\.]+\d+(?=[\s_\.]*(?:-+[\s_\.]*)[a-z]+))?.+?(?<!\d)(?:[\s_\.]*\(part[\s_\.]*[ivx]+\))?(?<isMovie>[\s_\.]*(?:[-!+]+[\s_\.]*)?(?:the[\s_\.]+)?movie)?(?:[\s_\.]*\(part[\s_\.]*[ivx]+\))?(?:[\s_\.]*\((?<year>(?:19|20)\d{2})\))?)(?<isTrailer>[\s_\.]*(?:character[\s_\.]*)?(?:cm|pv|menu))?[\s_\.]*(?:-+[\s_\.]*)?(?:(?:(?<isThemeSong>(?<![a-z])(?:nc)?(?:ed|op)[\s_\.]*))|(?<isSpecial>sp(?:ecial)?|s(?=\d+(?<!e)))|(?<isOther>\bO)|(?<isOVA>ova)(?:[\s_\.]+(?:[_-]+[\s_\.]*)?e|(?=e))|s(?:eason)?(?<season>\d+)(?:[\s_\.]+(?:[_-]+\.*)?e?|(?=e))|)(?:(?<!part[\s_\.]*)(?:(?<![a-z])e(?:ps?|pisodes?)?[\s_\.]*|#)?(?<episode>(?<!x[\. ]?|(?:flac|opus)[\. ]?(?:\d\.)?|\d+ - [\w\d \.]+)(?!19\d{2}|2\d{3}|\d+[pi])(?:\d+(?:(?!-\d+[pi])-+\d+?|\.5)?|(?<=(?:ed|op) *)\d+\.\d+)(?![\s_\.]*(?:[-!+]+[\s_\.]*)?(?:the[\s_\.]+)?movie))(?:(?<=(?:OP|ED)\d+)(?:\w|\.\d+)\b)?(?:[\s:\.]*end)?)(?:[\s:\.]*v(?<version>\d{1,2}))?(?! - (?:E(p(?:isode)?)?)? *\d+| OVA)(?:[\s_\.]*-*(?:[\s_\.]+(?<episodeName>(?!\d)[^([{\n]*?))?)?(?:[\s_\.]+(?:[\s_\.]+)?)?(?:[\s_.]*(?:\([^)]*\)|\[[^\]]*\]|{[^}]*}|[([{]+[^)\]}\n]*[)\]}]+|(?:(?<![a-z])(?:jpn?|jap(?:anese)?|en|eng(?:lish)?|es|(?:spa(?:nish)?|de|ger(?:man)?)|\d{3,4}[pi](?:-+hi\w*)?|(?:[uf]?hd|sd)|\d{3,4}x\d{3,4}|dual[\s_\.-]*audio|(?:www|web|bd|dvd|ld|blu[\s_\.-]*ray)(?:[\s_\.-]*(?:rip|dl))?|dl|rip|(?:av1|hevc|[hx]26[45])(?:-[a-z0-9]{1,6})?|(?:dolby(?:[\s_\.-]*(?:atmos|vision))?|dts|opus|e?ac3|aac|flac|dovi)(?:[\s\._]*[257]\.[0124](?:[_.-]+\w{1,6})?)?|(?:\w{2,3}[\s_\.-]*)?(?:sub(?:title)?s?|dub)|(?:un)?cen(?:\.|sored)?)[\s_\.]*){1,20})){0,20}[\s_\.]*(?:-[a-zA-Z0-9]+?)?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "foreign-1",
            Regex = new(
                @"^(?<showName>[^\]\n]+) - (?<episode>\d+) 「[^」\n]+」 \([^)\n]+\)\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "brackets-1",
            Regex = new(
                @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<year>\d{4})\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "brackets-2",
            Regex = new(
                @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "brackets-3",
            Regex = new(
                @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[?(?<showName>[^\]\n]+)\]? - (?<episode>\d+)(?: ?\[[^\]\n]+\] ?){0,20}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "reversed-1",
            Regex = new(
                @"^\[?(?<episode>\d+)\s*-\s*(?<showName>[^[]+])\s*(?:\[[^\]]*\])*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        // TODO: Add more rules here.
        new()
        {
            Name = "fallback",
            Regex = new(
                // Note: Currently it will not recognize episodes in the 19xx and 2xxx ranges. Let's hope nothing reaches that far.
                @"^(?:[{[(](?<releaseGroup>[^)}\]]+)[)}\]][\s_.]*)?(?<showName>(?<isMovie2>gekijouban[\s_.]+)?(?:[a-z]+[\s_\.]+\d+(?=[\s_\.]*(?:-+[\s_\.]*)[a-z]+))?.+?(?<!\d)(?:[\s_\.]*\(part[\s_\.]*[ivx]+\))?(?<isMovie>[\s_\.]*(?:[-!+]+[\s_\.]*)?(?:the[\s_\.]+)?movie)?(?:[\s_\.]*\(part[\s_\.]*[ivx]+\))?(?:[\s_\.]*\((?<year>(?:19|20)\d{2})\))?)(?<isTrailer>[\s_\.]*(?:character[\s_\.]*)?(?:cm|pv|menu))?[\s_\.]*(?:-+[\s_\.]*)?(?:(?:(?<isThemeSong>(?<![a-z])(?:nc)?(?:ed|op)[\s_\.]*))|(?<isSpecial>sp(?:ecial)?|s(?=\d+(?<!e)))|(?<isOther>\bO)|(?<isOVA>ova)(?:[\s_\.]+(?:[_-]+[\s_\.]*)?e|(?=e))|s(?:eason)?(?<season>\d+)(?:[\s_\.]+(?:[_-]+\.*)?e?|(?=e))|)(?:(?<!part[\s_\.]*)(?:(?<![a-z])e(?:ps?|pisodes?)?[\s_\.]*|#)?(?<episode>(?<!x[\. ]?|(?:flac|opus)[\. ]?(?:\d\.)?|\d+ - [\w\d \.]+)(?!19\d{2}|2\d{3}|\d+[pi])(?:\d+(?:(?!-\d+[pi])-+\d+?|\.5)?|(?<=(?:ed|op) *)\d+\.\d+)(?![\s_\.]*(?:[-!+]+[\s_\.]*)?(?:the[\s_\.]+)?movie))?(?:(?<=(?:OP|ED)\d+)(?:\w|\.\d+)\b)?(?:[\s:\.]*end)?)(?:[\s:\.]*v(?<version>\d{1,2}))?(?! - (?:E(p(?:isode)?)?)? *\d+| OVA)(?:[\s_\.]*-*(?:[\s_\.]+(?<episodeName>(?!\d)[^([{\n]*?))?)?(?:[\s_\.]+(?:[\s_\.]+)?)?(?:[\s_.]*(?:\([^)]*\)|\[[^\]]*\]|{[^}]*}|[([{]+[^)\]}\n]*[)\]}]+|(?:(?<![a-z])(?:jpn?|jap(?:anese)?|en|eng(?:lish)?|es|(?:spa(?:nish)?|de|ger(?:man)?)|\d{3,4}[pi](?:-+hi\w*)?|(?:[uf]?hd|sd)|\d{3,4}x\d{3,4}|dual[\s_\.-]*audio|(?:www|web|bd|dvd|ld|blu[\s_\.-]*ray)(?:[\s_\.-]*(?:rip|dl))?|dl|rip|(?:av1|hevc|[hx]26[45])(?:-[a-z0-9]{1,6})?|(?:dolby(?:[\s_\.-]*(?:atmos|vision))?|dts|opus|e?ac3|aac|flac|dovi)(?:[\s\._]*[257]\.[0124](?:[_.-]+\w{1,6})?)?|(?:\w{2,3}[\s_\.-]*)?(?:sub(?:title)?s?|dub)|(?:un)?cen(?:\.|sored)?)[\s_\.]*){1,20})){0,20}[\s_\.]*(?:-[a-zA-Z0-9]+?)?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
    ];

    private class MatchRule
    {
        public required string Name { get; init; }

        public required Regex Regex { get; init; }

        public Func<MatchRuleResult, Match, MatchRuleResult?> Transform { get; init; } = DefaultTransform;
    }
}
