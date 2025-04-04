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
    public int? Season { get; set; }

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
                var episodeParts = match.Groups["episode"].Value.Split('-');
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
                    Season = match.Groups["season"].Value is { Length: > 0 } ? int.Parse(match.Groups["season"].Value) : null,
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
        if (matchGroups["isSpecial"].Success)
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

    private static int ParseRomanNumerals(string romanNumeral)
        => romanNumeral.ToUpperInvariant() switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            "VI" => 6,
            "VII" => 7,
            "VIII" => 8,
            "IX" => 9,
            "X" => 10,
            "XI" => 11,
            "XII" => 12,
            "XIII" => 13,
            "XIV" => 14,
            "XV" => 15,
            "XVI" => 16,
            "XVII" => 17,
            "XVIII" => 18,
            "IXX" => 19,
            "XX" => 20,
            _ => -1,
        };

    private static MatchRuleResult? DefaultTransform(MatchRuleResult originalDetails, Match match)
    {
        var modifiedDetails = originalDetails with { };

        // Fix up show name by removing unwanted details and fixing spaces.
        if (modifiedDetails.ShowName is not null)
        {
            var showName = modifiedDetails.ShowName.Trim().Replace(_trimShowNameRegex, string.Empty);

            // Fix movie name when no episode number is provided.
            var episode = match.Groups["episode"].Value;
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
                if (showName.StartsWith("//"))
                    showName = $".{showName}";
            }

            var matchResult = showName.Match(@" S(\d+)$");
            if (matchResult.Success)
            {
                showName = showName[..^matchResult.Length];
                modifiedDetails.Season = int.Parse(matchResult.Groups[1].Value);
            }

            matchResult = showName.Match(@"\s+\b([IVX]+)\s*$");
            if (matchResult.Success)
            {
                var seasonNumber = ParseRomanNumerals(matchResult.Groups[1].Value);
                if (seasonNumber is not -1)
                {
                    showName = showName[..^matchResult.Length];
                    modifiedDetails.Season = seasonNumber;
                }
            }

            // Fix up year for some shows.
            matchResult = showName.Match(@" \d{4}$");
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
            if (modifiedDetails.Season != null && modifiedDetails.Season != 1 && !string.IsNullOrEmpty(episode) && string.IsNullOrEmpty(year))
                showName += $" S{modifiedDetails.Season}";

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

    private static readonly Regex _trimShowNameRegex = new(
        @"(?![\s_.]*\(part[\s_.]*[ivx]+\))(?![\s_.]*\((?:19|20)\d{2}\))(?:[\s_.]*(?:[([{][^)\]}\n]*[)\]}]|(?:(?<![a-z])(?:jpn?|jap(?:anese)?|en|eng(?:lish)?|es|(?:spa(?:nish)?|de|ger(?:man)?)|\d{3,4}[pi](?:-+hi\w*)?|(?:[uf]?hd|sd)|\d{3,4}x\d{3,4}|dual[\s_.-]*audio|(?:www|web|bd|dvd|ld|blu[\s_.-]*ray)(?:[\s_.-]*(?:rip|dl))?|dl|rip|(?:av1|hevc|[hx]26[45])(?:-[a-z0-9]{1,6})?|(?:dolby(?:[\s_.-]*atmos)?|dts|opus|ac3|aac|flac)(?:[\s._]*[257]\.[0124](?:[_.-]+\w{1,3})?)?|(?:\w{2,3}[\s_.-]*)?(?:sub(?:title)?s?|dub)|(?:un)?cen(?:\.|sored)?)[\s_.]*){1,20})){0,20}[\s_.]*$",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex _reStitchRegex = new(@"^[\s_.]*-+[\s_.]*$|^[\s_.]*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _themeSongCheckRegex = new(
        @"(?<![a-z0-9])(?:(?<isCreditless>nc|creditless)[\s_.]*)?(?<type>ed|op)(?![a-z]{2,})(?:[\s_.]*(?<episode>\d+(?!\d*p)))?(?<suffix>(?<=(?:OP|ED)(?:[\s_.]*\d+)?)(?:\.\d+|\w)\b)?",
        RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
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
        @"\b(?<source>(?:U?HD ?|SD ?)?TV|(?:U?HD ?)?(?:BD|Blue?-?Ray)|(?:H[KD] ?)?DVD|VHS|S?VCD|Web|www|LD|LaserDisc|camera|camcorder)s?(?:rip)?\b",
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
            Name = "trash-anime",
            Regex = new(
                @"^(?<showName>.+?(?: \((?<year>\d{4})\))) - (?:(?<isSpecial>S00?)|S\d+)E\d+(?:-E?\d+)? - (?<episode>\d+(?:-\d+)?) - (?<episodeName>.+?(?=\[)).*?(?:-(?<releaseGroup>[^\[\] ]+))?\s*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
                RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
        },
        new()
        {
            Name = "default",
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
