using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Renamer;

[RenamerID(RENAMER_ID)]
public class WebAOMRenamer : IRenamer<WebAOMSettings>
{
    private const string RENAMER_ID = "WebAOM";
    private readonly ILogger<WebAOMRenamer> _logger;
    private readonly IRelocationService _relocationService;

    public WebAOMRenamer(ILogger<WebAOMRenamer> logger, IRelocationService relocationService)
    {
        _logger = logger;
        _relocationService = relocationService;
    }

    public string Name => "WebAOM Renamer";
    public string Description => "The legacy renamer, based on WebAOM's renamer. You can find information for it at https://wiki.anidb.net/WebAOM#Scripting";

    public Shoko.Plugin.Abstractions.Events.RelocationResult GetNewPath(RelocationEventArgs<WebAOMSettings> args)
    {
        var script = args.Settings.Script;
        if (args.RenameEnabled && script == null)
        {
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = new RelocationError("No script available for renamer")
            };
        }

        string newFilename = null;
        var success = true;
        (IImportFolder dest, string folder) destination = default;
        Exception ex = null;
        try
        {
            if (args.RenameEnabled) (success, newFilename) = GetNewFileName(args, args.Settings, script);
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
        {
            return new Shoko.Plugin.Abstractions.Events.RelocationResult
            {
                Error = ex == null ? null : new RelocationError(ex.Message, ex)
            };
        }

        return new Shoko.Plugin.Abstractions.Events.RelocationResult
        {
            FileName = newFilename,
            DestinationImportFolder = destination.dest,
            Path = destination.folder
        };
    }

    public bool SupportsMoving => true;
    public bool SupportsRenaming => true;

    private readonly char[] validTests = "AGFEHXRTYDSCIZJWUMN".ToCharArray();

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
    Z   int     Video Bith Depth [8,10]
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
    Z   int     Video Bith Depth [8,10]
     */

    /// <summary>
    /// Test if the file belongs to the specified anime
    /// </summary>
    /// <param name="test"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestA(string test, List<SVR_AniDB_Episode> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (!int.TryParse(test, out var animeID))
            {
                return false;
            }

            if (notCondition)
            {
                return animeID != episodes[0].AnimeID;
            }

            return animeID == episodes[0].AnimeID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file belongs to the specified group
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestG(string test, SVR_AniDB_File aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var groupID = 0;

            //Leave groupID at 0 if "unknown".
            if (!test.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(test, out groupID))
                {
                    return false;
                }
            }

            if (notCondition)
            {
                return groupID != aniFile.GroupID;
            }

            return groupID == aniFile.GroupID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file is manually linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestM(string test, SVR_AniDB_File aniFile, List<SVR_AniDB_Episode> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!");

            // for a file to be manually linked it must NOT have an anifile, but does need episodes attached
            var manuallyLinked = false;
            if (aniFile == null)
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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file has any episodes linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestN(string test, SVR_AniDB_File aniFile, List<SVR_AniDB_Episode> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!");

            var epsLinked = aniFile == null && episodes != null && episodes.Count > 0;

            if (notCondition)
            {
                return !epsLinked;
            }

            return epsLinked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if this file has the specified Dub (audio) language
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestD(string test, SVR_AniDB_File aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            return notCondition
                ? aniFile.Languages.All(lan =>
                    !lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : aniFile.Languages.Any(lan =>
                    lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
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
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestS(string test, SVR_AniDB_File aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) &&
                aniFile.Subtitles.Count == 0)
            {
                return !notCondition;
            }

            return notCondition
                ? aniFile.Subtitles.All(lan =>
                    !lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : aniFile.Subtitles.Any(lan =>
                    lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this files is a specific version
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestF(string test, SVR_AniDB_File aniFile)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (aniFile == null)
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
                    return aniFile.FileVersion == version;
                }

                return aniFile.FileVersion != version;
            }

            if (greaterThan)
            {
                return aniFile.FileVersion > version;
            }

            if (greaterThanEqual)
            {
                return aniFile.FileVersion >= version;
            }

            if (lessThan)
            {
                return aniFile.FileVersion < version;
            }

            return aniFile.FileVersion <= version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this file is a specific bit depth
    /// </summary>
    /// <param name="test"></param>
    /// <param name="vid"></param>
    /// <returns></returns>
    private bool EvaluateTestZ(string test, SVR_VideoLocal vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testBitDepth))
            {
                return false;
            }

            if (vid.MediaInfo?.VideoStream == null)
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testBitDepth == vid.MediaInfo?.VideoStream?.BitDepth;
                }

                return testBitDepth != vid.MediaInfo?.VideoStream?.BitDepth;
            }

            if (greaterThan)
            {
                return vid.MediaInfo?.VideoStream?.BitDepth > testBitDepth;
            }

            if (greaterThanEqual)
            {
                return vid.MediaInfo?.VideoStream?.BitDepth >= testBitDepth;
            }

            if (lessThan)
            {
                return vid.MediaInfo?.VideoStream?.BitDepth < testBitDepth;
            }

            return vid.MediaInfo?.VideoStream?.BitDepth <= testBitDepth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestW(string test, SVR_VideoLocal vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (vid == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testWidth))
            {
                return false;
            }

            var width = SplitVideoResolution(vid.VideoResolution).Width;

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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestU(string test, SVR_VideoLocal vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (vid == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testHeight))
            {
                return false;
            }

            var height = SplitVideoResolution(vid.VideoResolution).Height;

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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }


    private bool EvaluateTestR(string test, SVR_AniDB_File aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            var hasSource = !string.IsNullOrEmpty(aniFile.File_Source);
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasSource)
            {
                return !notCondition;
            }


            if (test.Trim().Equals(aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestT(string test, SVR_AniDB_Anime anime)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var hasType = !string.IsNullOrEmpty(anime.GetAnimeTypeRAW());
            if (
                test.Trim()
                    .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasType)
            {
                return !notCondition;
            }


            if (test.Trim().Equals(anime.GetAnimeTypeRAW(), StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestY(string test, SVR_AniDB_Anime anime)
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
                    return anime.BeginYear == testYear;
                }

                return anime.BeginYear != testYear;
            }

            if (greaterThan)
            {
                return anime.BeginYear > testYear;
            }

            if (greaterThanEqual)
            {
                return anime.BeginYear >= testYear;
            }

            if (lessThan)
            {
                return anime.BeginYear < testYear;
            }

            return anime.BeginYear <= testYear;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestE(string test, List<SVR_AniDB_Episode> episodes)
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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestH(string test, List<SVR_AniDB_Episode> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var epType = string.Empty;
            switch (episodes[0].GetEpisodeTypeEnum())
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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Takes the the test parameters and checks for numerical operators
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
        if (test.Substring(0, 1).Equals("!"))
        {
            notCondition = true;
            test = test.Substring(1, test.Length - 1);
        }

        greaterThan = false;
        greaterThanEqual = false;
        if (test.Substring(0, 1).Equals(">"))
        {
            greaterThan = true;
            test = test.Substring(1, test.Length - 1);
            if (test.Substring(0, 1).Equals("="))
            {
                greaterThan = false;
                greaterThanEqual = true;
                test = test.Substring(1, test.Length - 1);
            }
        }

        lessThan = false;
        lessThanEqual = false;
        if (!test.Substring(0, 1).Equals("<"))
        {
            return;
        }

        lessThan = true;
        test = test.Substring(1, test.Length - 1);
        if (!test.Substring(0, 1).Equals("="))
        {
            return;
        }

        lessThan = false;
        lessThanEqual = true;
        test = test.Substring(1, test.Length - 1);
    }

    private bool EvaluateTestX(string test, SVR_AniDB_Anime anime)
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
                    return anime.EpisodeCountNormal == epCount;
                }

                return anime.EpisodeCountNormal != epCount;
            }

            if (greaterThan)
            {
                return anime.EpisodeCountNormal > epCount;
            }

            if (greaterThanEqual)
            {
                return anime.EpisodeCountNormal >= epCount;
            }

            if (lessThan)
            {
                return anime.EpisodeCountNormal < epCount;
            }

            return anime.EpisodeCountNormal <= epCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test whether the specified tag has a value
    /// </summary>
    /// <param name="test"></param>
    /// <param name="vid"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <param name="anime"></param>
    /// <returns></returns>
    private bool EvaluateTestI(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile,
        List<SVR_AniDB_Episode> episodes,
        SVR_AniDB_Anime anime)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }


            if (anime == null)
            {
                return false;
            }

            #region Test if Anime ID exists

            // Test if Anime ID exists

            var tagAnimeID = Constants.FileRenameTag.AnimeID.Substring(1,
                Constants.FileRenameTag.AnimeID.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (aniFile != null)
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

            var tagGroupID = Constants.FileRenameTag.GroupID.Substring(1,
                Constants.FileRenameTag.GroupID.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an group id
                if (aniFile != null)
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

            var tagOriginalFileName = Constants.FileRenameTag.OriginalFileName.Substring(1,
                Constants.FileRenameTag.OriginalFileName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagOriginalFileName, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Original File Name
                if (aniFile != null)
                {
                    if (string.IsNullOrEmpty(aniFile.FileName))
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
            var tagEpisodeNumber = Constants.FileRenameTag.EpisodeNumber.Substring(1,
                Constants.FileRenameTag.EpisodeNumber.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNumber, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Episode Number
                if (aniFile != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test file version

            // Test if Group Short Name exists - yes it always does
            var tagFileVersion = Constants.FileRenameTag.FileVersion.Substring(1,
                Constants.FileRenameTag.FileVersion.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagFileVersion, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (aniFile != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if ED2K Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KUpper = Constants.FileRenameTag.ED2KUpper.Substring(1,
                Constants.FileRenameTag.ED2KUpper.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagED2KUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if ED2K Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KLower = Constants.FileRenameTag.ED2KLower.Substring(1,
                Constants.FileRenameTag.ED2KLower.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagED2KLower, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if English title exists

            var tagAnimeNameEnglish = Constants.FileRenameTag.AnimeNameEnglish.Substring(1,
                Constants.FileRenameTag.AnimeNameEnglish.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.Titles.Any(ti =>
                        ti.Language == TitleLanguage.English &&
                        (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Kanji title exists

            var tagAnimeNameKanji = Constants.FileRenameTag.AnimeNameKanji.Substring(1,
                Constants.FileRenameTag.AnimeNameKanji.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameKanji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.Titles.Any(ti =>
                        ti.Language == TitleLanguage.Japanese &&
                        (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if anime main or romaji title exists

            var tagAnimeNameRomaji = Constants.FileRenameTag.AnimeNameMain.Substring(1,
                Constants.FileRenameTag.AnimeNameMain.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.Titles.Any(ti =>
                        ti.TitleType == TitleType.Main ||
                        (ti.Language == TitleLanguage.Romaji && ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if episode name (english) exists

            var tagEpisodeNameEnglish = Constants.FileRenameTag.EpisodeNameEnglish.Substring(1,
                Constants.FileRenameTag.EpisodeNameEnglish.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = RepoFactory.AniDB_Episode_Title
                    .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TitleLanguage.English)
                    .FirstOrDefault()?.Title;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if episode name (romaji) exists

            var tagEpisodeNameRomaji = Constants.FileRenameTag.EpisodeNameRomaji.Substring(1,
                Constants.FileRenameTag.EpisodeNameRomaji.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = RepoFactory.AniDB_Episode_Title
                    .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TitleLanguage.Romaji)
                    .FirstOrDefault()?.Title;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name short exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupShortName = Constants.FileRenameTag.GroupShortName.Substring(1,
                Constants.FileRenameTag.GroupShortName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupShortName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(aniFile?.Anime_GroupNameShort))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name long exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupLongName = Constants.FileRenameTag.GroupLongName.Substring(1,
                Constants.FileRenameTag.GroupLongName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupLongName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(aniFile?.Anime_GroupName))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCLower = Constants.FileRenameTag.CRCLower.Substring(1,
                Constants.FileRenameTag.CRCLower.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCRCLower, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = vid.CRC32;

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCUpper = Constants.FileRenameTag.CRCUpper.Substring(1,
                Constants.FileRenameTag.CRCUpper.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCRCUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = vid.CRC32;

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has an audio track

            var tagDubLanguage = Constants.FileRenameTag.DubLanguage.Substring(1,
                Constants.FileRenameTag.DubLanguage.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagDubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (aniFile == null || aniFile.Languages.Count == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a subtitle track

            var tagSubLanguage = Constants.FileRenameTag.SubLanguage.Substring(1,
                Constants.FileRenameTag.SubLanguage.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagSubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (aniFile == null || aniFile.Subtitles.Count == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Video resolution exists

            var tagVidRes = Constants.FileRenameTag.Resolution.Substring(1,
                Constants.FileRenameTag.Resolution.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVidRes, StringComparison.InvariantCultureIgnoreCase))
            {
                var vidRes = string.Empty;

                if (string.IsNullOrEmpty(vidRes) && vid != null)
                {
                    vidRes = vid.VideoResolution;
                }

                if (string.IsNullOrEmpty(vidRes))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a video codec defined

            var tagVideoCodec = Constants.FileRenameTag.VideoCodec.Substring(1,
                Constants.FileRenameTag.VideoCodec.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVideoCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has an audio codec defined

            var tagAudioCodec = Constants.FileRenameTag.AudioCodec.Substring(1,
                Constants.FileRenameTag.AudioCodec.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAudioCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has Video Bit Depth defined

            var tagVideoBitDepth = Constants.FileRenameTag.VideoBitDepth.Substring(1,
                Constants.FileRenameTag.VideoBitDepth.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVideoBitDepth, StringComparison.InvariantCultureIgnoreCase))
            {
                var bitDepthExists = vid?.MediaInfo?.VideoStream != null && vid.MediaInfo?.VideoStream?.BitDepth != 0;
                if (!bitDepthExists)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if censored

            var tagCensored = Constants.FileRenameTag.Censored.Substring(1,
                Constants.FileRenameTag.Censored.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCensored, StringComparison.InvariantCultureIgnoreCase))
            {
                var isCensored = false;
                if (aniFile != null)
                {
                    isCensored = aniFile.IsCensored ?? false;
                }

                if (!isCensored)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Deprecated

            var tagDeprecated = Constants.FileRenameTag.Deprecated.Substring(1,
                Constants.FileRenameTag.Deprecated.Length - 1); // remove % at the front
            if (!test.Trim().Equals(tagDeprecated, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            var isDeprecated = false;
            if (aniFile != null)
            {
                isDeprecated = aniFile.IsDeprecated;
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
            _logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private (bool success, string name) GetNewFileName(RelocationEventArgs args, WebAOMSettings settings, string script)
    {
        // Cheat and just look it up by location to avoid rewriting this whole file.
        var sourceFolder = RepoFactory.ImportFolder.GetAll()
            .FirstOrDefault(a => args.File.Path.StartsWith(a.ImportFolderLocation));
        if (sourceFolder == null) return (false, "Unable to Get Import Folder");

        var place = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(
            args.File.Path.Replace(sourceFolder.ImportFolderLocation, ""), sourceFolder.ImportFolderID);
        var vid = place?.VideoLocal;
        var lines = script.Split(Environment.NewLine.ToCharArray());

        var newFileName = string.Empty;

        var episodes = new List<SVR_AniDB_Episode>();
        SVR_AniDB_Anime anime;

        if (vid == null) return (false, "Unable to access file");

        // get all the data so we don't need to get multiple times
        var aniFile = vid.AniDBFile;
        if (aniFile == null)
        {
            var animeEps = vid.AnimeEpisodes;
            if (animeEps.Count == 0) return (false, "Unable to get episode for file");

            episodes.AddRange(animeEps.Select(a => a.AniDB_Episode).OrderBy(a => a?.EpisodeType ?? (int)EpisodeType.Other)
                .ThenBy(a => a.EpisodeNumber));

            anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
            if (anime == null) return (false, "Unable to get anime for file");
        }
        else
        {
            episodes = aniFile.Episodes;
            if (episodes.Count == 0) return (false, "Unable to get episode for file");

            anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
            if (anime == null) return (false, "Unable to get anime for file");
        }

        foreach (var line in lines)
        {
            var thisLine = line.Trim();
            if (thisLine.Length == 0) continue;

            // remove all comments from this line
            var comPos = thisLine.IndexOf("//", StringComparison.Ordinal);
            if (comPos >= 0) thisLine = thisLine.Substring(0, comPos);


            // check if this line has no tests (applied to all files)
            if (thisLine.StartsWith(Constants.FileRenameReserved.Do, StringComparison.InvariantCultureIgnoreCase))
            {
                var action = GetAction(thisLine);
                var (success, name) = PerformActionOnFileName(newFileName, action, settings, vid, aniFile, episodes, anime);
                if (!success) return (false, name);
                newFileName = name;
            }
            else if (EvaluateTest(thisLine, vid, aniFile, episodes, anime))
            {
                // if the line has passed the tests, then perform the action
                var action = GetAction(thisLine);

                // if the action is fail, we don't want to rename
                if (action.ToUpper().Trim().Equals(Constants.FileRenameReserved.Fail, StringComparison.InvariantCultureIgnoreCase))
                    return (false, "The script called FAIL");

                var (success, name) = PerformActionOnFileName(newFileName, action, settings, vid, aniFile, episodes, anime);
                if (!success) return (false, name);
                newFileName = name;
            }
        }

        if (string.IsNullOrEmpty(newFileName)) return (false, "The new filename is empty (script error)");

        var pathToVid = place.FilePath;
        if (string.IsNullOrEmpty(pathToVid)) return (false, "Unable to get the file's old filename");

        var ext = Path.GetExtension(pathToVid); // Prefer VideoLocal_Place as this is more accurate.
        if (string.IsNullOrEmpty(ext)) return (false, "Unable to get the file's extension"); // fail if we get a blank extension, something went wrong.

        // finally add back the extension
        return (true, $"{newFileName.Replace("`", "'")}{ext}".ReplaceInvalidPathCharacters());
    }

    private (bool, string) PerformActionOnFileName(string newFileName, string action, WebAOMSettings settings, SVR_VideoLocal vid, SVR_AniDB_File aniFile, List<SVR_AniDB_Episode> episodes, SVR_AniDB_Anime anime)
    {
        // find the first test
        var posStart = action.IndexOf(' ');
        if (posStart < 0) return (true, newFileName);

        var actionType = action[..posStart];
        var parameter = action.Substring(posStart + 1, action.Length - posStart - 1);

        // action is to add the new file name
        if (actionType.Trim().Equals(Constants.FileRenameReserved.Add, StringComparison.InvariantCultureIgnoreCase))
            return PerformActionOnFileNameADD(newFileName, parameter, settings, vid, aniFile, episodes, anime);

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

    private (bool, string) PerformActionOnFileNameADD(string newFileName, string action, WebAOMSettings settings, SVR_VideoLocal vid,
        SVR_AniDB_File aniFile, List<SVR_AniDB_Episode> episodes, SVR_AniDB_Anime anime)
    {
        newFileName += action;
        newFileName = newFileName.Replace("'", string.Empty);

        #region Anime ID

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeID.ToLower()))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeID, anime.AnimeID.ToString());
        }

        #endregion

        #region English title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameEnglish.ToLower()))
        {
            var title = anime.Titles.FirstOrDefault(ti => ti.Language == TitleLanguage.English && ti.TitleType is TitleType.Main or TitleType.Official)?.Title;
            if (string.IsNullOrEmpty(title))
                return (false, "Unable to get the English title");
            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameEnglish, title);
        }

        #endregion

        #region Anime main title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameMain.ToLower()))
        {
            var title = anime.Titles
                            .FirstOrDefault(ti => ti.TitleType == TitleType.Main || (ti.Language == TitleLanguage.Romaji && ti.TitleType == TitleType.Official))
                            ?.Title ??
                        anime.MainTitle;
            if (string.IsNullOrEmpty(title))
                return (false, "Unable to get the main title");

            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameMain, title);
        }

        #endregion

        #region Kanji title

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameKanji.ToLower()))
        {
            var title = anime.Titles.FirstOrDefault(ti => ti.Language == TitleLanguage.Japanese && ti.TitleType is TitleType.Main or TitleType.Official)?.Title;
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

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Credits)
            {
                prefix = "C";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Other)
            {
                prefix = "O";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Parody)
            {
                prefix = "P";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Special)
            {
                prefix = "S";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Trailer)
            {
                prefix = "T";
            }

            switch (episodes[0].GetEpisodeTypeEnum())
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCountNormal;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCountSpecial;
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

            switch (episodes[0].GetEpisodeTypeEnum())
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCountNormal;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCountSpecial;
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
            var epname = RepoFactory.AniDB_Episode_Title
                .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TitleLanguage.English)
                .FirstOrDefault()?.Title;
            if (string.IsNullOrEmpty(epname)) return (false, "Unable to get the english episode name");
            if (epname.Length > settings.MaxEpisodeLength) epname = epname[..(settings.MaxEpisodeLength - 1)] + "…";

            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameEnglish, epname);
        }

        #endregion

        #region Episode name (romaji)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNameRomaji.ToLower()))
        {
            var epname = RepoFactory.AniDB_Episode_Title
                .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TitleLanguage.Romaji)
                .FirstOrDefault()?.Title;
            if (string.IsNullOrEmpty(epname)) return (false, "Unable to get the romaji episode name");
            if (epname.Length > settings.MaxEpisodeLength) epname = epname[..(settings.MaxEpisodeLength - 1)] + "…";

            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameRomaji, epname);
        }

        #endregion

        #region sub group name (short)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupShortName.ToLower()))
        {
            var subgroup = aniFile?.Anime_GroupNameShort ?? "Unknown";
            if (subgroup.Equals("raw", StringComparison.InvariantCultureIgnoreCase)) subgroup = "Unknown";

            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupShortName, subgroup);
        }

        #endregion

        #region sub group name (long)

        if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupLongName.ToLower()))
        {
            var subgroup = aniFile?.Anime_GroupName ?? "Unknown";
            if (subgroup.Equals("raw", StringComparison.InvariantCultureIgnoreCase)) subgroup = "Unknown";
            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupLongName, subgroup);
        }

        #endregion

        #region ED2k hash (upper)

        if (action.Trim().Contains(Constants.FileRenameTag.ED2KUpper))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.ED2KUpper, vid.Hash.ToUpper());
        }

        #endregion

        #region ED2k hash (lower)

        if (action.Trim().Contains(Constants.FileRenameTag.ED2KLower))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.ED2KLower, vid.Hash.ToLower());
        }

        #endregion

        #region CRC (upper)

        if (action.Trim().Contains(Constants.FileRenameTag.CRCUpper))
        {
            var crc = vid.CRC32;

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
            var crc = vid.CRC32;

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
            newFileName = newFileName.Replace(Constants.FileRenameTag.FileVersion, aniFile?.FileVersion.ToString() ?? "1");
        }

        #endregion

        #region Audio languages (dub)

        if (action.Trim().Contains(Constants.FileRenameTag.DubLanguage))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.DubLanguage, aniFile?.LanguagesRAW ?? string.Empty);
        }

        #endregion

        #region Subtitle languages (sub)

        if (action.Trim().Contains(Constants.FileRenameTag.SubLanguage))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.SubLanguage, aniFile?.SubtitlesRAW ?? string.Empty);
        }

        #endregion

        #region Video Codec

        if (action.Trim().Contains(Constants.FileRenameTag.VideoCodec))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.VideoCodec, vid?.MediaInfo?.VideoStream?.CodecID);
        }

        #endregion

        #region Audio Codec

        if (action.Trim().Contains(Constants.FileRenameTag.AudioCodec))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.AudioCodec, vid?.MediaInfo?.AudioStreams.FirstOrDefault()?.CodecID);
        }

        #endregion

        #region Video Bit Depth

        if (action.Trim().Contains(Constants.FileRenameTag.VideoBitDepth))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.VideoBitDepth, (vid?.MediaInfo?.VideoStream?.BitDepth).ToString());
        }

        #endregion

        #region Video Source

        if (action.Trim().Contains(Constants.FileRenameTag.Source))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.Source, aniFile?.File_Source ?? "Unknown");
        }

        #endregion

        #region Anime Type

        if (action.Trim().Contains(Constants.FileRenameTag.Type))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.Type, anime.GetAnimeTypeRAW());
        }

        #endregion

        #region Video Resolution

        if (action.Trim().Contains(Constants.FileRenameTag.Resolution))
        {
            var res = string.Empty;
            // try the video info
            if (vid != null)
            {
                res = vid.VideoResolution;
            }

            newFileName = newFileName.Replace(Constants.FileRenameTag.Resolution, res.Trim());
        }

        #endregion

        #region Video Height

        if (action.Trim().Contains(Constants.FileRenameTag.VideoHeight))
        {
            var res = string.Empty;
            // try the video info
            if (vid != null)
            {
                res = vid.VideoResolution;
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
            newFileName = newFileName.Replace(Constants.FileRenameTag.Year, anime.BeginYear.ToString());
        }

        #endregion

        #region File ID

        if (action.Trim().Contains(Constants.FileRenameTag.FileID))
        {
            if (aniFile != null)
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.FileID, aniFile.FileID.ToString());
            }
        }

        #endregion

        #region Episode ID

        if (action.Trim().Contains(Constants.FileRenameTag.EpisodeID))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeID, episodes[0].EpisodeID.ToString());
        }

        #endregion

        #region Group ID

        if (action.Trim().Contains(Constants.FileRenameTag.GroupID))
        {
            newFileName = newFileName.Replace(Constants.FileRenameTag.GroupID, aniFile?.GroupID.ToString() ?? "Unknown");
        }

        #endregion

        #region Original File Name

        if (action.Trim().Contains(Constants.FileRenameTag.OriginalFileName))
        {
            // remove the extension first
            if (aniFile != null)
            {
                var ext = Path.GetExtension(aniFile.FileName);
                var partial = aniFile.FileName.Substring(0, aniFile.FileName.Length - ext.Length);

                newFileName = newFileName.Replace(Constants.FileRenameTag.OriginalFileName, partial);
            }
        }

        #endregion

        #region Censored

        if (action.Trim().Contains(Constants.FileRenameTag.Censored))
        {
            var censored = "cen";
            if (aniFile?.IsCensored ?? false) censored = "unc";

            newFileName = newFileName.Replace(Constants.FileRenameTag.Censored, censored);
        }

        #endregion

        #region Deprecated

        if (action.Trim().Contains(Constants.FileRenameTag.Deprecated))
        {
            var depr = "New";
            if (aniFile?.IsDeprecated ?? false) depr = "DEPR";

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

    private bool EvaluateTest(string line, SVR_VideoLocal vid, SVR_AniDB_File aniFile,
        List<SVR_AniDB_Episode> episodes,
        SVR_AniDB_Anime anime)
    {
        line = line.Trim();
        // determine if this line has a test
        foreach (var c in validTests)
        {
            var prefix = $"IF {c}(";
            if (!line.ToUpper().StartsWith(prefix)) continue;

            // find the first test
            var posStart = line.IndexOf('(');
            var posEnd = line.IndexOf(')');
            var posStartOrig = posStart;

            if (posEnd < posStart) return false;

            var condition = line.Substring(posStart + 1, posEnd - posStart - 1);
            var passed = EvaluateTest(c, condition, vid, aniFile, episodes, anime);

            // check for OR's and AND's
            while (posStart > 0)
            {
                posStart = line.IndexOf(';', posStart);
                if (posStart <= 0) continue;

                var thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                // remove any spacing
                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                var thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

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
                var thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

                if (thisPassed) return true;

                posStart += 1;
            }
        }

        return false;
    }

    private bool EvaluateTest(char testChar, string testCondition, SVR_VideoLocal vid,
        SVR_AniDB_File aniFile,
        List<SVR_AniDB_Episode> episodes, SVR_AniDB_Anime anime)
    {
        testCondition = testCondition.Trim();

        switch (testChar)
        {
            case 'A':
                return EvaluateTestA(testCondition, episodes);
            case 'G':
                return EvaluateTestG(testCondition, aniFile);
            case 'D':
                return EvaluateTestD(testCondition, aniFile);
            case 'S':
                return EvaluateTestS(testCondition, aniFile);
            case 'F':
                return EvaluateTestF(testCondition, aniFile);
            case 'R':
                return EvaluateTestR(testCondition, aniFile);
            case 'Z':
                return EvaluateTestZ(testCondition, vid);
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
                return false;
            case 'J':
                return false;
            case 'I':
                return EvaluateTestI(testCondition, vid, aniFile, episodes, anime);
            case 'W':
                return EvaluateTestW(testCondition, vid);
            case 'U':
                return EvaluateTestU(testCondition, vid);
            case 'M':
                return EvaluateTestM(testCondition, aniFile, episodes);
            case 'N':
                return EvaluateTestN(testCondition, aniFile, episodes);
            default:
                return false;
        }
    }

    public (IImportFolder dest, string folder) GetDestinationFolder(RelocationEventArgs<WebAOMSettings> args)
    {
        if (args.Settings.GroupAwareSorting)
            return GetGroupAwareDestination(args);

        return GetFlatFolderDestination(args);
    }

    private (IImportFolder dest, string folder) GetGroupAwareDestination(RelocationEventArgs<WebAOMSettings> args)
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
        var name = series.PreferredTitle.ReplaceInvalidPathCharacters();
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
            var groupName = group.PreferredTitle.ReplaceInvalidPathCharacters();
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

    private static bool ValidDestinationFolder(IImportFolder dest)
    {
        return dest.DropFolderType.HasFlag(DropFolderType.Destination);
    }

    private (IImportFolder dest, string folder) GetFlatFolderDestination(RelocationEventArgs args)
    {
        if (_relocationService.GetExistingSeriesLocationWithSpace(args) is { } existingSeriesLocation)
            return existingSeriesLocation;

        if (_relocationService.GetFirstDestinationWithSpace(args) is { } firstDestinationWithSpace)
        {
            var series = args.Series.Select(s => s.AnidbAnime).FirstOrDefault();
            if (series is null)
                return (null, "Series not found");
            return (firstDestinationWithSpace, series.PreferredTitle.ReplaceInvalidPathCharacters());
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

    public WebAOMSettings DefaultSettings => new()
    {
        GroupAwareSorting = false,
        Script = """
// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv
// Sub group name
DO ADD '[%grp] '
// Anime Name, use english name if it exists, otherwise use the Romaji name
IF I(eng) DO ADD '%eng '
IF I(ann);I(!eng) DO ADD '%ann '
// Episode Number, don't use episode number for movies
IF T(!Movie) DO ADD '- %enr'
// If the file version is v2 or higher add it here
IF F(!1) DO ADD 'v%ver'
// Video Resolution
DO ADD ' (%res'
// Video Source (only if blu-ray or DVD)
IF R(DVD),R(Blu-ray) DO ADD ' %src'
// Video Codec
DO ADD ' %vid'
// Video Bit Depth (only if 10bit)
IF Z(10) DO ADD ' %bitbit'
DO ADD ') '
DO ADD '[%CRC]'

// Replacement rules (cleanup)
DO REPLACE ' ' '_' // replace spaces with underscores
DO REPLACE 'H264/AVC' 'H264'
DO REPLACE '0x0' ''
DO REPLACE '__' '_'
DO REPLACE '__' '_'

// Replace all illegal file name characters
DO REPLACE '<' '('
DO REPLACE '>' ')'
DO REPLACE ':' '-'
DO REPLACE '" + (char)34 +' '`'
DO REPLACE '/' '_'
DO REPLACE '/' '_'
DO REPLACE '\\' '_'
DO REPLACE '|' '_'
DO REPLACE '?' '_'
DO REPLACE '*' '_'
"""
    };
}
