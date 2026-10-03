using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Plugin.WebAOM;

/// <summary>
///   The legacy renamer, which names and places files by a WebAOM script.
/// </summary>
/// <param name="_logger">The logger.</param>
/// <param name="_relocationService">The relocation service, for the destinations with free space.</param>
public class WebAOMRenamer(ILogger<WebAOMRenamer> _logger, IVideoRelocationService _relocationService) : IRelocationProvider<WebAOMSettings>
{
    private static readonly char[] _validTests = "AGFEHXRTYDSCIZJWUMN".ToCharArray();

    /// <inheritdoc/>
    public string Name => "WebAOM Renamer";

    /// <inheritdoc/>
    public string Description => """
      The legacy renamer, based on WebAOM's renamer. You can find information
      for it at https://wiki.anidb.net/WebAOM#Scripting
    """;

    /// <inheritdoc/>
    public RelocationResult GetPath(RelocationContext<WebAOMSettings> args)
    {
        var script = args.Configuration.Script;
        if (args.RenameEnabled && string.IsNullOrEmpty(script))
            return new() { Error = new RelocationError("No script available for renamer") };

        string? newFilename = null;
        var success = true;
        (IManagedFolder? dest, string? folder)? destination = default;
        Exception? ex = null;
        try
        {
            if (args.RenameEnabled) (success, newFilename) = GetNewFileName(args, args.Configuration, script);
            if (args.MoveEnabled)
            {
                destination = GetDestinationFolder(args);
                if (destination == default) success = false;
            }
        }
        catch (Exception e)
        {
            success = false;
            ex = e;
        }

        if (!success)
            return new() { Error = ex == null ? null : new RelocationError(ex.Message, ex) };

        return new()
        {
            FileName = newFilename,
            ManagedFolder = destination?.dest,
            Path = destination?.folder,
        };
    }

    /* TESTS
    A   int     Anime id
    G   int     Group id
    F   int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
    E   text    Episode number
    H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
    X   text    Total number of episodes
    R   text    Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
    T   text    Type [unknown, TV, OVA, Movie, Other, web]
    Y   int     Year
    D   text    Dub language (one of the audio tracks) [japanese, english, ...]
    S   text    Sub language (one of the subtitle tracks) [japanese, english, ...]
    C   text    Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
    J   text    Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
    I   text    Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
    Z   int     Video Bit Depth [8,10]
    W   int     Video Resolution Width [720, 1280, 1920, ...]
    U   int     Video Resolution Height [576, 720, 1080, ...]
    M   null    empty - test whether the file is manually linked
     */

    /* TESTS - Alphabetical
    A   int     Anime id
    C   text    Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
    D   text    Dub language (one of the audio tracks) [japanese, english, ...]
    E   text    Episode number
    F   int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
    G   int     Group id
    H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
    I   text    Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
    J   text    Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
    M   null    empty - test whether the file is manually linked
    N   null    empty - test whether the file has any episodes linked to it
    R   text    Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
    S   text    Sub language (one of the subtitle tracks) [japanese, english, ...]
    T   text    Type [unknown, TV, OVA, Movie, Other, web]
    U   int     Video Resolution Height [576, 720, 1080, ...]
    W   int     Video Resolution Width [720, 1280, 1920, ...]
    X   text    Total number of episodes
    Y   int     Year
    Z   int     Video Bit Depth [8,10]
     */

    /// <summary>
    /// Test if the file belongs to the specified anime
    /// </summary>
    /// <param name="test"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestA(string test, IReadOnlyList<IAnidbEpisode> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            if (!int.TryParse(test, out var animeID))
            {
                return false;
            }

            if (notCondition)
            {
                return animeID != episodes[0].AnidbAnimeID;
            }

            return animeID == episodes[0].AnidbAnimeID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file belongs to the specified group
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <returns></returns>
    private bool EvaluateTestG(string test, IReleaseInfo? releaseInfo)
    {
        if (releaseInfo is null)
        {
            return false;
        }
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            if (notCondition)
            {
                return test != releaseInfo.Group?.ID;
            }

            return test == releaseInfo.Group?.ID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file is manually linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestM(string test, IReleaseInfo? releaseInfo, IReadOnlyList<IAnidbEpisode> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Length > 0 && test[..1].Equals("!");

            // for a file to be manually linked it must NOT have an ani file, but does need episodes attached
            var manuallyLinked = false;
            if (releaseInfo == null)
            {
                manuallyLinked = true;
                if (episodes == null || episodes.Count == 0)
                {
                    manuallyLinked = false;
                }
            }

            if (notCondition)
            {
                return !manuallyLinked;
            }

            return manuallyLinked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file has any episodes linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestN(string test, IReleaseInfo? releaseInfo, IReadOnlyList<IAnidbEpisode> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Length > 0 && test[..1].Equals("!");

            var epsLinked = releaseInfo == null && episodes != null && episodes.Count > 0;

            if (notCondition)
            {
                return !epsLinked;
            }

            return epsLinked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if this file has the specified Dub (audio) language
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <returns></returns>
    private bool EvaluateTestD(string test, IReleaseInfo? releaseInfo)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            if (releaseInfo == null)
            {
                return false;
            }

            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) &&
                (releaseInfo.MediaInfo?.AudioLanguages?.Count ?? 0) == 0)
            {
                return !notCondition;
            }

            return notCondition
                ? (releaseInfo.MediaInfo?.AudioLanguages ?? []).All(lan =>
                    !lan.GetString().Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : (releaseInfo.MediaInfo?.AudioLanguages ?? []).Any(lan =>
                    lan.GetString().Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this files has the specified Sub (subtitle) language
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <returns></returns>
    private bool EvaluateTestS(string test, IReleaseInfo? releaseInfo)
    {
        try
        {
            var notCondition = false;
            if (test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            if (releaseInfo == null)
            {
                return false;
            }

            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) &&
                (releaseInfo.MediaInfo?.SubtitleLanguages?.Count ?? 0) == 0)
            {
                return !notCondition;
            }

            return notCondition
                ? (releaseInfo.MediaInfo?.SubtitleLanguages ?? []).All(lan =>
                    !lan.GetString().Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : (releaseInfo.MediaInfo?.SubtitleLanguages ?? []).Any(lan =>
                    lan.GetString().Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this files is a specific version
    /// </summary>
    /// <param name="test"></param>
    /// <param name="releaseInfo"></param>
    /// <returns></returns>
    private bool EvaluateTestF(string test, IReleaseInfo? releaseInfo)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (releaseInfo == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var version))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return releaseInfo.Version == version;
                }

                return releaseInfo.Version != version;
            }

            if (greaterThan)
            {
                return releaseInfo.Version > version;
            }

            if (greaterThanEqual)
            {
                return releaseInfo.Version >= version;
            }

            if (lessThan)
            {
                return releaseInfo.Version < version;
            }

            return releaseInfo.Version <= version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this file is a specific bit depth
    /// </summary>
    /// <param name="test"></param>
    /// <param name="video"></param>
    /// <returns></returns>
    private bool EvaluateTestZ(string test, IVideo video)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testBitDepth))
            {
                return false;
            }

            if (video.MediaInfo?.VideoStream == null)
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testBitDepth == video.MediaInfo?.VideoStream?.BitDepth;
                }

                return testBitDepth != video.MediaInfo?.VideoStream?.BitDepth;
            }

            if (greaterThan)
            {
                return video.MediaInfo?.VideoStream?.BitDepth > testBitDepth;
            }

            if (greaterThanEqual)
            {
                return video.MediaInfo?.VideoStream?.BitDepth >= testBitDepth;
            }

            if (lessThan)
            {
                return video.MediaInfo?.VideoStream?.BitDepth < testBitDepth;
            }

            return video.MediaInfo?.VideoStream?.BitDepth <= testBitDepth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestW(string test, IVideo video)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (video == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testWidth))
            {
                return false;
            }

            var width = SplitVideoResolution(GetVideoResolution(video)).Width;

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testWidth == width;
                }

                return testWidth != width;
            }

            if (greaterThan)
            {
                return width > testWidth;
            }

            if (greaterThanEqual)
            {
                return width >= testWidth;
            }

            if (lessThan)
            {
                return width < testWidth;
            }

            return width <= testWidth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestU(string test, IVideo video)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (video == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testHeight))
            {
                return false;
            }

            var height = SplitVideoResolution(GetVideoResolution(video)).Height;

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testHeight == height;
                }

                return testHeight != height;
            }

            if (greaterThan)
            {
                return height > testHeight;
            }

            if (greaterThanEqual)
            {
                return height >= testHeight;
            }

            if (lessThan)
            {
                return height < testHeight;
            }

            return height <= testHeight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }


    private bool EvaluateTestR(string test, IReleaseInfo? releaseInfo)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            if (releaseInfo == null)
            {
                return false;
            }

            var hasSource = !string.IsNullOrEmpty(GetLegacySource(releaseInfo.Source));
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasSource)
            {
                return !notCondition;
            }


            if (test.Trim().Equals(GetLegacySource(releaseInfo.Source), StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestC(string test, IVideo video)
    {
        try
        {
            bool notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (video == null) return false;

            // Video codecs
            IMediaInfo? mediaInfo = video.MediaInfo;
            var hasSource = !string.IsNullOrEmpty(mediaInfo?.VideoStream?.Codec.Simplified);
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasSource)
            {
                return !notCondition;
            }

            if (test.Trim().Equals(mediaInfo?.VideoStream?.Codec.Simplified, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestJ(string test, IVideo video)
    {
        try
        {
            bool notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (video == null) return false;

            // Audio codecs
            IMediaInfo? mediaInfo = video.MediaInfo;
            var hasSource = !string.IsNullOrEmpty(mediaInfo?.AudioStreams.FirstOrDefault()?.Codec.Simplified);
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasSource)
            {
                return !notCondition;
            }

            var codec = mediaInfo?.AudioStreams.FirstOrDefault()?.Codec.Simplified;

            if (test.Trim().Equals(codec, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestT(string test, IAnidbAnime anime)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            var hasType = !string.IsNullOrEmpty(GetRawAnimeType(anime.Type));
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasType)
            {
                return !notCondition;
            }


            if (test.Trim().Equals(GetRawAnimeType(anime.Type), StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestY(string test, IAnidbAnime anime)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testYear))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return GetBeginYear(anime) == testYear;
                }

                return GetBeginYear(anime) != testYear;
            }

            if (greaterThan)
            {
                return GetBeginYear(anime) > testYear;
            }

            if (greaterThanEqual)
            {
                return GetBeginYear(anime) >= testYear;
            }

            if (lessThan)
            {
                return GetBeginYear(anime) < testYear;
            }

            return GetBeginYear(anime) <= testYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestE(string test, IReadOnlyList<IAnidbEpisode> episodes)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testEpNumber))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return episodes[0].EpisodeNumber == testEpNumber;
                }

                return episodes[0].EpisodeNumber != testEpNumber;
            }

            if (greaterThan)
            {
                return episodes[0].EpisodeNumber > testEpNumber;
            }

            if (greaterThanEqual)
            {
                return episodes[0].EpisodeNumber >= testEpNumber;
            }

            if (lessThan)
            {
                return episodes[0].EpisodeNumber < testEpNumber;
            }

            return episodes[0].EpisodeNumber <= testEpNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestH(string test, IReadOnlyList<IAnidbEpisode> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }

            var epType = string.Empty;
            switch (episodes[0].Type)
            {
                case EpisodeType.Episode:
                    epType = "E";
                    break;
                case EpisodeType.Credits:
                    epType = "C";
                    break;
                case EpisodeType.Other:
                    epType = "O";
                    break;
                case EpisodeType.Parody:
                    epType = "P";
                    break;
                case EpisodeType.Special:
                    epType = "S";
                    break;
                case EpisodeType.Trailer:
                    epType = "T";
                    break;
            }


            if (test.Trim().Equals(epType, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Takes the test parameters and checks for numerical operators
    /// Removes the operators from the test string passed
    /// </summary>
    /// <param name="test"></param>
    /// <param name="notCondition"></param>
    /// <param name="greaterThan"></param>
    /// <param name="greaterThanEqual"></param>
    /// <param name="lessThan"></param>
    /// <param name="lessThanEqual"></param>
    private void ProcessNumericalOperators(ref string test, out bool notCondition, out bool greaterThan,
        out bool greaterThanEqual, out bool lessThan, out bool lessThanEqual)
    {
        notCondition = false;
        if (test.Length > 0 && test[..1].Equals("!"))
        {
            notCondition = true;
            test = test[1..];
        }

        greaterThan = false;
        greaterThanEqual = false;
        if (test[..1].Equals(">"))
        {
            greaterThan = true;
            test = test[1..];
            if (test[..1].Equals("="))
            {
                greaterThan = false;
                greaterThanEqual = true;
                test = test[1..];
            }
        }

        lessThan = false;
        lessThanEqual = false;
        if (!test[..1].Equals("<"))
        {
            return;
        }

        lessThan = true;
        test = test[1..];
        if (!test[..1].Equals("="))
        {
            return;
        }

        lessThan = false;
        lessThanEqual = true;
        test = test[1..];
    }

    private bool EvaluateTestX(string test, IAnidbAnime anime)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var epCount))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return anime.EpisodeCounts.Episodes == epCount;
                }

                return anime.EpisodeCounts.Episodes != epCount;
            }

            if (greaterThan)
            {
                return anime.EpisodeCounts.Episodes > epCount;
            }

            if (greaterThanEqual)
            {
                return anime.EpisodeCounts.Episodes >= epCount;
            }

            if (lessThan)
            {
                return anime.EpisodeCounts.Episodes < epCount;
            }

            return anime.EpisodeCounts.Episodes <= epCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test whether the specified tag has a value
    /// </summary>
    /// <param name="test"></param>
    /// <param name="video"></param>
    /// <param name="releaseInfo"></param>
    /// <param name="episodes"></param>
    /// <param name="anime"></param>
    /// <returns></returns>
    private bool EvaluateTestI(string test, IVideo video, IReleaseInfo? releaseInfo,
        IReadOnlyList<IAnidbEpisode> episodes,
        IAnidbAnime anime)
    {
        try
        {
            var notCondition = false;
            if (test.Length > 0 && test[..1].Equals("!"))
            {
                notCondition = true;
                test = test[1..];
            }


            if (anime == null)
            {
                return false;
            }

            #region Test if Anime ID exists

            // Test if Anime ID exists

            var tagAnimeID = Constants.FileRenameTag.AnimeID[1..]; // remove % at the front
            if (test.Trim().Equals(tagAnimeID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (releaseInfo != null)
                {
                    if (notCondition)
                    {
                        return false;
                    }

                    return true;
                }

                if (notCondition)
                {
                    return true;
                }

                return false;
            }

            #endregion

            #region Test if Group ID exists

            // Test if Group ID exists

            var tagGroupID = Constants.FileRenameTag.GroupID[1..]; // remove % at the front
            if (test.Trim().Equals(tagGroupID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an group id
                if (releaseInfo != null)
                {
                    if (notCondition)
                    {
                        return false;
                    }

                    return true;
                }

                if (notCondition)
                {
                    return false;
                }

                return true;
            }

            #endregion

            #region Test if Original File Name exists

            // Test if Original File Nameexists

            var tagOriginalFileName = Constants.FileRenameTag.OriginalFileName[1..]; // remove % at the front
            if (test.Trim().Equals(tagOriginalFileName, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Original File Name
                if (releaseInfo != null)
                {
                    if (string.IsNullOrEmpty(releaseInfo.OriginalFilename))
                    {
                        return notCondition;
                    }

                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Episode Number exists

            // Test if Episode Number exists
            var tagEpisodeNumber = Constants.FileRenameTag.EpisodeNumber[1..]; // remove % at the front
            if (test.Trim().Equals(tagEpisodeNumber, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Episode Number
                if (releaseInfo != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test file version

            // Test if Group Short Name exists - yes it always does
            var tagFileVersion = Constants.FileRenameTag.FileVersion[1..]; // remove % at the front
            if (test.Trim().Equals(tagFileVersion, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (releaseInfo != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if ED2K Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KUpper = Constants.FileRenameTag.ED2KUpper[1..]; // remove % at the front
            if (test.Trim().Equals(tagED2KUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if ED2K Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KLower = Constants.FileRenameTag.ED2KLower[1..]; // remove % at the front
            if (test.Trim().Equals(tagED2KLower, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if English title exists

            var tagAnimeNameEnglish = Constants.FileRenameTag.AnimeNameEnglish[1..]; // remove % at the front
            if (test.Trim().Equals(tagAnimeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                if (GetAnidbTitles(anime).Any(ti =>
                        ti.Language == TitleLanguage.English &&
                        (ti.Type == TitleType.Main || ti.Type == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Kanji title exists

            var tagAnimeNameKanji = Constants.FileRenameTag.AnimeNameKanji[1..]; // remove % at the front
            if (test.Trim().Equals(tagAnimeNameKanji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (GetAnidbTitles(anime).Any(ti =>
                        ti.Language == TitleLanguage.Japanese &&
                        (ti.Type == TitleType.Main || ti.Type == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if anime main or romaji title exists

            var tagAnimeNameRomaji = Constants.FileRenameTag.AnimeNameMain[1..]; // remove % at the front
            if (test.Trim().Equals(tagAnimeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (GetAnidbTitles(anime).Any(ti =>
                        ti.Type == TitleType.Main ||
                        (ti.Language == TitleLanguage.Romaji && ti.Type == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if episode name (english) exists

            var tagEpisodeNameEnglish = Constants.FileRenameTag.EpisodeNameEnglish[1..]; // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = episodes[0].DefaultTitle.Value;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if episode name (romaji) exists

            var tagEpisodeNameRomaji = Constants.FileRenameTag.EpisodeNameRomaji[1..]; // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = GetAnidbTitles(episodes[0]).FirstOrDefault(ti => ti.Language == TitleLanguage.Romaji)?.Value;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name short exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupShortName = Constants.FileRenameTag.GroupShortName[1..]; // remove % at the front
            if (test.Trim().Equals(tagGroupShortName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(releaseInfo?.Group?.ShortName))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name long exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupLongName = Constants.FileRenameTag.GroupLongName[1..]; // remove % at the front
            if (test.Trim().Equals(tagGroupLongName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(releaseInfo?.Group?.Name))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCLower = Constants.FileRenameTag.CRCLower[1..]; // remove % at the front
            if (test.Trim().Equals(tagCRCLower, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = GetCrc32(video);

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCUpper = Constants.FileRenameTag.CRCUpper[1..]; // remove % at the front
            if (test.Trim().Equals(tagCRCUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = GetCrc32(video);

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has an audio track

            var tagDubLanguage = Constants.FileRenameTag.DubLanguage[1..]; // remove % at the front
            if (test.Trim().Equals(tagDubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (releaseInfo == null || (releaseInfo.MediaInfo?.AudioLanguages?.Count ?? 0) == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a subtitle track

            var tagSubLanguage = Constants.FileRenameTag.SubLanguage[1..]; // remove % at the front
            if (test.Trim().Equals(tagSubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (releaseInfo == null || (releaseInfo.MediaInfo?.SubtitleLanguages?.Count ?? 0) == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Video resolution exists

            var tagVidRes = Constants.FileRenameTag.Resolution[1..]; // remove % at the front
            if (test.Trim().Equals(tagVidRes, StringComparison.InvariantCultureIgnoreCase))
            {
                var vidRes = string.Empty;

                if (string.IsNullOrEmpty(vidRes) && video != null)
                {
                    vidRes = GetVideoResolution(video);
                }

                if (string.IsNullOrEmpty(vidRes))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a video codec defined

            var tagVideoCodec = Constants.FileRenameTag.VideoCodec[1..]; // remove % at the front
            if (test.Trim().Equals(tagVideoCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has an audio codec defined

            var tagAudioCodec = Constants.FileRenameTag.AudioCodec[1..]; // remove % at the front
            if (test.Trim().Equals(tagAudioCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has Video Bit Depth defined

            var tagVideoBitDepth = Constants.FileRenameTag.VideoBitDepth[1..]; // remove % at the front
            if (test.Trim().Equals(tagVideoBitDepth, StringComparison.InvariantCultureIgnoreCase))
            {
                var bitDepthExists = video?.MediaInfo?.VideoStream != null && video.MediaInfo?.VideoStream?.BitDepth != 0;
                if (!bitDepthExists)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if censored

            var tagCensored = Constants.FileRenameTag.Censored[1..]; // remove % at the front
            if (test.Trim().Equals(tagCensored, StringComparison.InvariantCultureIgnoreCase))
            {
                var isCensored = false;
                if (releaseInfo != null)
                {
                    isCensored = releaseInfo.IsCensored ?? false;
                }

                if (!isCensored)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Deprecated

            var tagDeprecated = Constants.FileRenameTag.Deprecated[1..]; // remove % at the front
            if (!test.Trim().Equals(tagDeprecated, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            var isDeprecated = false;
            if (releaseInfo != null)
            {
                isDeprecated = releaseInfo.IsDeprecated;
            }

            if (!isDeprecated)
            {
                return notCondition;
            }

            return !notCondition;

            #endregion=
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encountered an exception while executing test: {Message}", ex.ToString());
            return false;
        }
    }

    private (bool success, string name) GetNewFileName(RelocationContext args, WebAOMSettings settings, string script)
    {
        var video = args.Video;
        var file = args.File;
        if (video is null || file is null) return (false, "Unable to access file");

        var lines = script.Split(Environment.NewLine.ToCharArray());

        var newFileName = string.Empty;

        // get all the data so we don't need to get multiple times
        var releaseInfo = video.ReleaseInfo;
        if (args.Episodes.Count == 0) return (false, "Unable to get episode for file");

        var episodes = args.Episodes
            .Select(episode => episode.AnidbEpisode)
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();

        var anime = episodes[0].Series;
        if (anime is null) return (false, "Unable to get anime for file");

        foreach (var line in lines)
        {
            var thisLine = line.Trim();
            if (thisLine.Length == 0) continue;

            // remove all comments from this line
            var comPos = thisLine.IndexOf("//", StringComparison.Ordinal);
            if (comPos >= 0) thisLine = thisLine[..comPos];


            // check if this line has no tests (applied to all files)
            if (thisLine.StartsWith(Constants.FileRenameReserved.Do, StringComparison.InvariantCultureIgnoreCase))
            {
                var action = GetAction(thisLine);
                var (success, name) = PerformActionOnFileName(newFileName, action, settings, video, releaseInfo, episodes, anime);
                if (!success) return (false, name);
                newFileName = name;
            }
            else if (EvaluateTest(thisLine, video, releaseInfo, episodes, anime))
            {
                // if the line has passed the tests, then perform the action
                var action = GetAction(thisLine);

                // if the action is fail, we don't want to rename
                if (action.ToUpper().Trim().Equals(Constants.FileRenameReserved.Fail, StringComparison.InvariantCultureIgnoreCase))
                    return (false, "The script called FAIL");

                var (success, name) = PerformActionOnFileName(newFileName, action, settings, video, releaseInfo, episodes, anime);
                if (!success) return (false, name);
                newFileName = name;
            }
        }

        if (string.IsNullOrEmpty(newFileName)) return (false, "The new filename is empty (script error)");

        var pathToVid = file.RelativePath;
        if (string.IsNullOrEmpty(pathToVid)) return (false, "Unable to get the file's old filename");

        var ext = Path.GetExtension(pathToVid); // Prefer the file location as this is more accurate.
        if (string.IsNullOrEmpty(ext)) return (false, "Unable to get the file's extension"); // fail if we get a blank extension, something went wrong.

        // finally add back the extension
        return (true, $"{newFileName.Replace("`", "'")}{ext}".ReplaceInvalidPathCharacters());
    }

    private (bool, string) PerformActionOnFileName(string newFileName, string action, WebAOMSettings settings, IVideo video, IReleaseInfo? releaseInfo, IReadOnlyList<IAnidbEpisode> episodes, IAnidbAnime anime)
    {
        // find the first test
        var posStart = action.IndexOf(' ');
        if (posStart < 0) return (true, newFileName);

        var actionType = action[..posStart];
        var parameter = action.Substring(posStart + 1, action.Length - posStart - 1);

        // action is to add the new file name
        if (actionType.Trim().Equals(Constants.FileRenameReserved.Add, StringComparison.InvariantCultureIgnoreCase))
            return PerformActionOnFileNameADD(newFileName, parameter, settings, video, releaseInfo, episodes, anime);

        if (actionType.Trim().Equals(Constants.FileRenameReserved.Replace, StringComparison.InvariantCultureIgnoreCase))
            return PerformActionOnFileNameREPLACE(ref newFileName, parameter);

        return (true, newFileName);
    }

    private (bool, string) PerformActionOnFileNameREPLACE(ref string newFileName, string action)
    {
        try
        {
            action = action.Trim();

            var posStart1 = action.IndexOf('\'', 0);
            if (posStart1 < 0) return (false, "Unable to parse replace string");

            var posEnd1 = action.IndexOf('\'', posStart1 + 1);
            if (posEnd1 < 0) return (false, "Unable to parse replace string");

            var toReplace = action.Substring(posStart1 + 1, posEnd1 - posStart1 - 1);
            if (string.IsNullOrEmpty(toReplace)) return (false, "Replace string cannot be empty");

            var posStart2 = action.IndexOf('\'', posEnd1 + 1);
            if (posStart2 < 0) return (false, "Unable to parse replace with string");

            var posEnd2 = action.IndexOf('\'', posStart2 + 1);
            if (posEnd2 < 0) return (false, "Unable to parse replace with string");

            var replaceWith = action.Substring(posStart2 + 1, posEnd2 - posStart2 - 1);

            newFileName = newFileName.Replace(toReplace, replaceWith);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return (false, ex.Message);
        }

        return (true, newFileName);
    }

    private (bool, string) PerformActionOnFileNameADD(string newFileName, string action, WebAOMSettings settings, IVideo video,
        IReleaseInfo? releaseInfo, IReadOnlyList<IAnidbEpisode> episodes, IAnidbAnime anime)
    {
        newFileName += action;
        newFileName = newFileName.Replace("'", string.Empty);

        #region Anime ID

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeID.ToLower()))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeID, anime.AnidbID.ToString());
        }

        #endregion

        #region English title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameEnglish.ToLower()))
        {
            var title = GetAnidbTitles(anime).FirstOrDefault(ti => ti.Language == TitleLanguage.English && ti.Type is TitleType.Main or TitleType.Official)?.Value;
            if (string.IsNullOrEmpty(title))
                return (false, "Unable to get the English title");
            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameEnglish, title);
        }

        #endregion

        #region Anime main title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameMain.ToLower()))
        {
            var title = GetAnidbTitles(anime)
                            .FirstOrDefault(ti => ti.Type == TitleType.Main || (ti.Language == TitleLanguage.Romaji && ti.Type == TitleType.Official))
                            ?.Value ??
                        anime.DefaultTitle.Value;
            if (string.IsNullOrEmpty(title))
                return (false, "Unable to get the main title");

            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameMain, title);
        }

        #endregion

        #region Kanji title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameKanji.ToLower()))
        {
            var title = GetAnidbTitles(anime).FirstOrDefault(ti => ti.Language == TitleLanguage.Japanese && ti.Type is TitleType.Main or TitleType.Official)?.Value;
            if (string.IsNullOrEmpty(title))
                return (false, "Unable to get the kanji title");

            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameKanji, title);
        }

        #endregion

        #region Episode Number

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNumber.ToLower()))
        {
            var prefix = string.Empty;
            int epCount;

            if (episodes[0].Type == EpisodeType.Credits)
            {
                prefix = "C";
            }

            if (episodes[0].Type == EpisodeType.Other)
            {
                prefix = "O";
            }

            if (episodes[0].Type == EpisodeType.Parody)
            {
                prefix = "P";
            }

            if (episodes[0].Type == EpisodeType.Special)
            {
                prefix = "S";
            }

            if (episodes[0].Type == EpisodeType.Trailer)
            {
                prefix = "T";
            }

            switch (episodes[0].Type)
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCounts.Episodes;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCounts.Specials;
                    break;
                case EpisodeType.Credits:
                case EpisodeType.Trailer:
                case EpisodeType.Parody:
                case EpisodeType.Other:
                    epCount = 1;
                    break;
                default:
                    epCount = 1;
                    break;
            }

            var zeroPadding = Math.Max(epCount.ToString().Length, 2);

            // normal episode
            var episodeNumber = prefix + episodes[0].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');

            if (episodes.Count > 1)
            {
                episodeNumber += "-" + episodes[^1].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
            }

            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNumber, episodeNumber);
        }

        #endregion

        #region Episode Number

        if (action.Trim().Contains(Constants.FileRenameTag.Episodes, StringComparison.CurrentCultureIgnoreCase))
        {
            int epCount;

            switch (episodes[0].Type)
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCounts.Episodes;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCounts.Specials;
                    break;
                case EpisodeType.Credits:
                case EpisodeType.Trailer:
                case EpisodeType.Parody:
                case EpisodeType.Other:
                    epCount = 1;
                    break;
                default:
                    epCount = 1;
                    break;
            }

            var zeroPadding = epCount.ToString().Length;

            var episodeNumber = epCount.ToString().PadLeft(zeroPadding, '0');

            newFileName = newFileName.Replace(Constants.FileRenameTag.Episodes, episodeNumber);
        }

        #endregion

        #region Episode name (english)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNameEnglish.ToLower()))
        {
            var epname = episodes[0].DefaultTitle.Value;
            if (string.IsNullOrEmpty(epname)) return (false, "Unable to get the english episode name");
            if (epname.Length > settings.MaxEpisodeLength) epname = epname[..(settings.MaxEpisodeLength - 1)] + "…";

            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameEnglish, epname);
        }

        #endregion

        #region Episode name (romaji)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNameRomaji.ToLower()))
        {
            var epname = GetAnidbTitles(episodes[0]).FirstOrDefault(ti => ti.Language == TitleLanguage.Romaji)?.Value;
            if (string.IsNullOrEmpty(epname)) return (false, "Unable to get the romaji episode name");
            if (epname.Length > settings.MaxEpisodeLength) epname = epname[..(settings.MaxEpisodeLength - 1)] + "…";

            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameRomaji, epname);
        }

        #endregion

        #region sub group name (short)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupShortName.ToLower()))
        {
            var subgroup = releaseInfo?.Group?.ShortName ?? "Unknown";
            if (subgroup.Equals("raw", StringComparison.InvariantCultureIgnoreCase)) subgroup = "Unknown";

            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupShortName, subgroup);
        }

        #endregion

        #region sub group name (long)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupLongName.ToLower()))
        {
            var subgroup = releaseInfo?.Group?.Name ?? "Unknown";
            if (subgroup.Equals("raw", StringComparison.InvariantCultureIgnoreCase)) subgroup = "Unknown";
            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupLongName, subgroup);
        }

        #endregion

        #region ED2k hash (upper)

        if (action.Trim().Contains(Constants.FileRenameTag.ED2KUpper))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.ED2KUpper, video.ED2K.ToUpper());
        }

        #endregion

        #region ED2k hash (lower)

        if (action.Trim().Contains(Constants.FileRenameTag.ED2KLower))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.ED2KLower, video.ED2K.ToLower());
        }

        #endregion

        #region CRC (upper)

        if (action.Trim().Contains(Constants.FileRenameTag.CRCUpper))
        {
            var crc = GetCrc32(video);

            if (!string.IsNullOrEmpty(crc))
            {
                crc = crc.ToUpper();
                newFileName = newFileName.Replace(Constants.FileRenameTag.CRCUpper, crc);
            }
        }

        #endregion

        #region CRC (lower)

        if (action.Trim().Contains(Constants.FileRenameTag.CRCLower))
        {
            var crc = GetCrc32(video);

            if (!string.IsNullOrEmpty(crc))
            {
                crc = crc.ToLower();
                newFileName = newFileName.Replace(Constants.FileRenameTag.CRCLower, crc);
            }
        }

        #endregion

        #region File Version

        if (action.Trim().Contains(Constants.FileRenameTag.FileVersion))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.FileVersion, releaseInfo?.Version.ToString() ?? "1");
        }

        #endregion

        #region Audio languages (dub)

        if (action.Trim().Contains(Constants.FileRenameTag.DubLanguage))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.DubLanguage, releaseInfo?.MediaInfo?.AudioLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty);
        }

        #endregion

        #region Subtitle languages (sub)

        if (action.Trim().Contains(Constants.FileRenameTag.SubLanguage))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.SubLanguage, releaseInfo?.MediaInfo?.SubtitleLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty);
        }

        #endregion

        #region Video Codec

        if (action.Trim().Contains(Constants.FileRenameTag.VideoCodec))
        {
            IMediaInfo? mediaInfo = video.MediaInfo;
            newFileName = newFileName.Replace(Constants.FileRenameTag.VideoCodec, mediaInfo?.VideoStream?.Codec.Simplified);
        }

        #endregion

        #region Audio Codec

        if (action.Trim().Contains(Constants.FileRenameTag.AudioCodec))
        {
            IMediaInfo? mediaInfo = video.MediaInfo;
            newFileName = newFileName.Replace(Constants.FileRenameTag.AudioCodec, mediaInfo?.AudioStreams.FirstOrDefault()?.Codec.Simplified);
        }

        #endregion

        #region Video Bit Depth

        if (action.Trim().Contains(Constants.FileRenameTag.VideoBitDepth))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.VideoBitDepth, (video?.MediaInfo?.VideoStream?.BitDepth).ToString());
        }

        #endregion

        #region Video Source

        if (action.Trim().Contains(Constants.FileRenameTag.Source))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.Source, releaseInfo is null ? "Unknown" : GetLegacySource(releaseInfo.Source));
        }

        #endregion

        #region Anime Type

        if (action.Trim().Contains(Constants.FileRenameTag.Type))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.Type, GetRawAnimeType(anime.Type) ?? "unknown");
        }

        #endregion

        #region Video Resolution

        if (action.Trim().Contains(Constants.FileRenameTag.Resolution))
        {
            var res = string.Empty;
            // try the video info
            if (video != null)
            {
                res = GetVideoResolution(video);
            }

            newFileName = newFileName.Replace(Constants.FileRenameTag.Resolution, res.Trim());
        }

        #endregion

        #region Video Height

        if (action.Trim().Contains(Constants.FileRenameTag.VideoHeight))
        {
            var res = string.Empty;
            // try the video info
            if (video != null)
            {
                res = GetVideoResolution(video);
            }

            var reses = res.Split('x');
            if (reses.Length > 1)
            {
                res = reses[1];
            }

            newFileName = newFileName.Replace(Constants.FileRenameTag.VideoHeight, res);
        }

        #endregion

        #region Year

        if (action.Trim().Contains(Constants.FileRenameTag.Year))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.Year, GetBeginYear(anime).ToString());
        }

        #endregion

        #region File ID

        if (action.Trim().Contains(Constants.FileRenameTag.FileID))
        {
            if (releaseInfo is { ReleaseURI: not null } && releaseInfo.ReleaseURI.StartsWith(AnidbReleasePrefix))
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.FileID, releaseInfo.ReleaseURI[AnidbReleasePrefix.Length..]);
            }
        }

        #endregion

        #region Episode ID

        if (action.Trim().Contains(Constants.FileRenameTag.EpisodeID))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeID, episodes[0].AnidbID.ToString());
        }

        #endregion

        #region Group ID

        if (action.Trim().Contains(Constants.FileRenameTag.GroupID))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupID, releaseInfo?.Group?.ID ?? "Unknown");
        }

        #endregion

        #region Original File Name

        if (action.Trim().Contains(Constants.FileRenameTag.OriginalFileName))
        {
            // remove the extension first
            if (!string.IsNullOrWhiteSpace(releaseInfo?.OriginalFilename))
            {
                var ext = Path.GetExtension(releaseInfo.OriginalFilename);
                var partial = releaseInfo.OriginalFilename[..^ext.Length];

                newFileName = newFileName.Replace(Constants.FileRenameTag.OriginalFileName, partial);
            }
        }

        #endregion

        #region Censored

        if (action.Trim().Contains(Constants.FileRenameTag.Censored))
        {
            var censored = "cen";
            if (!(releaseInfo?.IsCensored ?? false)) censored = "unc";

            newFileName = newFileName.Replace(Constants.FileRenameTag.Censored, censored);
        }

        #endregion

        #region Deprecated

        if (action.Trim().Contains(Constants.FileRenameTag.Deprecated))
        {
            var depr = "New";
            if (releaseInfo?.IsDeprecated ?? false) depr = "DEPR";

            newFileName = newFileName.Replace(Constants.FileRenameTag.Deprecated, depr);
        }

        #endregion

        return (true, newFileName);
    }

    private string GetAction(string line)
    {
        // find the first test
        var posStart = line.IndexOf("DO ", StringComparison.Ordinal);
        if (posStart < 0) return string.Empty;

        var action = line.Substring(posStart + 3, line.Length - posStart - 3);
        return action;
    }

    private bool EvaluateTest(string line, IVideo video, IReleaseInfo? releaseInfo,
        IReadOnlyList<IAnidbEpisode> episodes,
        IAnidbAnime anime)
    {
        line = line.Trim();
        // determine if this line has a test
        foreach (var c in _validTests)
        {
            var prefix = $"IF {c}(";
            if (!line.ToUpper().StartsWith(prefix)) continue;

            // find the first test
            var posStart = line.IndexOf('(');
            var posEnd = line.IndexOf(')');
            var posStartOrig = posStart;

            if (posEnd < posStart) return false;

            var condition = line.Substring(posStart + 1, posEnd - posStart - 1);
            var passed = EvaluateTest(c, condition, video, releaseInfo, episodes, anime);

            // check for OR's and AND's
            while (posStart > 0)
            {
                posStart = line.IndexOf(';', posStart);
                if (posStart <= 0) continue;

                var thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                // remove any spacing
                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                var thisTest = thisLineRemainder[..1].ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, video, releaseInfo, episodes, anime);

                if (!passed || !thisPassed) return false;

                posStart += 1;
            }

            // if the first test passed, and we only have OR's left then it is an automatic success
            if (passed)
            {
                return true;
            }

            posStart = posStartOrig;
            while (posStart > 0)
            {
                posStart = line.IndexOf(',', posStart);
                if (posStart <= 0) continue;

                var thisLineRemainder =
                    line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                // remove any spacing
                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                var thisTest = thisLineRemainder[..1].ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, video, releaseInfo, episodes, anime);

                if (thisPassed) return true;

                posStart += 1;
            }
        }

        return false;
    }

    private bool EvaluateTest(char testChar, string testCondition, IVideo video,
        IReleaseInfo? releaseInfo,
        IReadOnlyList<IAnidbEpisode> episodes, IAnidbAnime anime)
    {
        testCondition = testCondition.Trim();

        switch (testChar)
        {
            case 'A':
                return EvaluateTestA(testCondition, episodes);
            case 'G':
                return EvaluateTestG(testCondition, releaseInfo);
            case 'D':
                return EvaluateTestD(testCondition, releaseInfo);
            case 'S':
                return EvaluateTestS(testCondition, releaseInfo);
            case 'F':
                return EvaluateTestF(testCondition, releaseInfo);
            case 'R':
                return EvaluateTestR(testCondition, releaseInfo);
            case 'Z':
                return EvaluateTestZ(testCondition, video);
            case 'T':
                return EvaluateTestT(testCondition, anime);
            case 'Y':
                return EvaluateTestY(testCondition, anime);
            case 'E':
                return EvaluateTestE(testCondition, episodes);
            case 'H':
                return EvaluateTestH(testCondition, episodes);
            case 'X':
                return EvaluateTestX(testCondition, anime);
            case 'C':
                return EvaluateTestC(testCondition, video);
            case 'J':
                return EvaluateTestJ(testCondition, video);
            case 'I':
                return EvaluateTestI(testCondition, video, releaseInfo, episodes, anime);
            case 'W':
                return EvaluateTestW(testCondition, video);
            case 'U':
                return EvaluateTestU(testCondition, video);
            case 'M':
                return EvaluateTestM(testCondition, releaseInfo, episodes);
            case 'N':
                return EvaluateTestN(testCondition, releaseInfo, episodes);
            default:
                return false;
        }
    }

    private (IManagedFolder? dest, string? folder)? GetDestinationFolder(RelocationContext<WebAOMSettings> args)
    {
        if (args.Configuration.GroupAwareSorting)
            return GetGroupAwareDestination(args);

        return GetFlatFolderDestination(args);
    }

    private (IManagedFolder? dest, string folder) GetGroupAwareDestination(RelocationContext<WebAOMSettings> args)
    {
        if (args?.Episodes == null || args.Episodes.Count == 0)
        {
            throw new ArgumentException("File is unrecognized. Not Moving");
        }

        // get the series
        var series = args.Series.FirstOrDefault();

        if (series == null)
        {
            throw new ArgumentException("Series cannot be found for file");
        }

        // replace the invalid characters
        var name = series.Title.ReplaceInvalidPathCharacters();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Series Name is null or empty");
        }

        var group = args.Groups.FirstOrDefault();
        if (group == null)
        {
            throw new ArgumentException("Group could not be found for file");
        }

        string path;
        if (group.Series.Count == 1)
        {
            path = name;
        }
        else
        {
            var groupName = group.Title.ReplaceInvalidPathCharacters();
            path = Path.Combine(groupName, name);
        }

        var destFolder = series.Restricted switch
        {
            true => args.AvailableFolders.FirstOrDefault(a =>
                a.Path.Contains("Hentai", StringComparison.InvariantCultureIgnoreCase) &&
                ValidDestinationFolder(a)) ?? args.AvailableFolders.FirstOrDefault(ValidDestinationFolder),
            false => args.AvailableFolders.FirstOrDefault(a =>
                !a.Path.Contains("Hentai", StringComparison.InvariantCultureIgnoreCase) &&
                ValidDestinationFolder(a))
        };

        return (destFolder, path);
    }

    private static bool ValidDestinationFolder(IManagedFolder? dest)
    {
        return dest?.DropFolderType.HasFlag(DropFolderType.Destination) ?? false;
    }

    private (IManagedFolder? dest, string? folder)? GetFlatFolderDestination(RelocationContext args)
    {
        if (_relocationService.GetExistingSeriesLocationWithSpace(args) is { } existingSeriesLocation)
            return existingSeriesLocation;

        if (_relocationService.GetFirstDestinationWithSpace(args) is { } firstDestinationWithSpace)
        {
            var series = args.Series.Select(s => s.AnidbAnime).FirstOrDefault();
            if (series is null)
                return (null, "Series not found");
            return (firstDestinationWithSpace, series.Title.ReplaceInvalidPathCharacters());
        }

        return (null, "Unable to resolve a destination");
    }

    private static (int Width, int Height) SplitVideoResolution(string resolution)
    {
        var videoWidth = 0;
        var videoHeight = 0;
        if (resolution.Trim().Length > 0)
        {
            var dimensions = resolution.Split('x');
            if (dimensions.Length > 1)
            {
                int.TryParse(dimensions[0], out videoWidth);
                int.TryParse(dimensions[1], out videoHeight);
            }
        }

        return (videoWidth, videoHeight);
    }

    #region Helpers

    /// <summary>
    ///   The prefix of an AniDB release URI, followed by the file's ID.
    /// </summary>
    private const string AnidbReleasePrefix = "https://anidb.net/file/";

    /// <summary>
    ///   The titles AniDB itself gave an entry, without the ones other sources
    ///   or users added to it.
    /// </summary>
    /// <param name="entry">The AniDB anime or episode.</param>
    /// <returns>The titles, in AniDB's own order.</returns>
    private static IReadOnlyList<ITitle> GetAnidbTitles(IWithTitles entry)
        => [.. entry.Titles.Where(title => title.Source == MetadataSource.AniDB)];

    /// <summary>
    ///   The year the anime began airing, or <c>0</c> when it has no air date.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The year.</returns>
    private static int GetBeginYear(IAnidbAnime anime)
        => anime.AirDate?.Year ?? 0;

    /// <summary>
    ///   The video's CRC32 hash, or an empty string when it was not computed.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <returns>The hash.</returns>
    private static string GetCrc32(IVideo video)
        => video.Hashes.FirstOrDefault(hash => hash.Type is "CRC32")?.Value ?? string.Empty;

    /// <summary>
    ///   The video's resolution as <c>{width}x{height}</c>, or <c>0x0</c>
    ///   when it has no video stream.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <returns>The resolution.</returns>
    private static string GetVideoResolution(IVideo video)
        => video.MediaInfo?.VideoStream is { } stream ? $"{stream.Width}x{stream.Height}" : "0x0";

    /// <summary>
    ///   The source of a release as AniDB used to name it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The name.</returns>
    private static string GetLegacySource(ReleaseSource source) => source switch
    {
        ReleaseSource.TV => "tv",
        ReleaseSource.Web => "www",
        ReleaseSource.DVD => "dvd",
        ReleaseSource.BluRay => "bluray",
        ReleaseSource.VHS => "vhs",
        ReleaseSource.Camera => "camcorder",
        ReleaseSource.VCD => "vcd",
        ReleaseSource.LaserDisc => "ld",
        ReleaseSource.Film => "film",
        _ => "unk",
    };

    /// <summary>
    ///   The type of an anime as AniDB names it.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The name, or <c>null</c> when the type is unknown.</returns>
    private static string? GetRawAnimeType(AnimeType type) => type switch
    {
        AnimeType.Movie => "movie",
        AnimeType.OVA => "ova",
        AnimeType.TV => "tv series",
        AnimeType.TVSpecial => "tv special",
        AnimeType.Web => "web",
        AnimeType.Other => "other",
        AnimeType.MusicVideo => "music video",
        _ => null,
    };

    #endregion
}
