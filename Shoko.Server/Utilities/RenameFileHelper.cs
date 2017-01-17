using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Shoko.Models.Server;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server
{
    public class RenameFileHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly char[] validTests = "AGFEHXRTYDSCIZJWUMN".ToCharArray();
        /* TESTS
		A	int     Anime id
		G	int     Group id
		F	int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
		E	text	Episode number
		H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
		X	text	Total number of episodes
		R	text	Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
		T	text	Type [unknown, TV, OVA, Movie, Other, web]
		Y	int    	Year
		D	text	Dub language (one of the audio tracks) [japanese, english, ...]
		S	text	Sub language (one of the subtitle tracks) [japanese, english, ...]
		C   text	Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
		J   text	Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
		I	text	Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
		Z	int 	Video Bith Depth [8,10]
		W	int 	Video Resolution Width [720, 1280, 1920, ...]
		U	int 	Video Resolution Height [576, 720, 1080, ...]
		M	null 	empty - test whether the file is manually linked
		 */

        /* TESTS - Alphabetical
		A	int     Anime id
		C   text	Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
		D	text	Dub language (one of the audio tracks) [japanese, english, ...]
		E	text	Episode number
		F	int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
		G	int     Group id
		H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
		I	text	Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
		J   text	Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
		M	null 	empty - test whether the file is manually linked
		N	null 	empty - test whether the file has any episodes linked to it
		R	text	Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
		S	text	Sub language (one of the subtitle tracks) [japanese, english, ...]
		T	text	Type [unknown, TV, OVA, Movie, Other, web]
		U	int 	Video Resolution Height [576, 720, 1080, ...]
		W	int 	Video Resolution Width [720, 1280, 1920, ...]
		X	text	Total number of episodes
		Y	int    	Year
		Z	int 	Video Bith Depth [8,10]
		 */

        /// <summary>
        /// Test if the file belongs to the specified anime
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        private static bool EvaluateTestA(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile, List<AniDB_Episode> episodes)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                int animeID = 0;
                int.TryParse(test, out animeID);

                if (notCondition)
                    return animeID != episodes[0].AnimeID;
                else
                    return animeID == episodes[0].AnimeID;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test if the file belongs to the specified group
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <param name="aniFile"></param>
        /// <returns></returns>
        private static bool EvaluateTestG(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                int groupID = 0;

                //Leave groupID at 0 if "unknown".
                if (String.Compare(test, "unknown", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    int.TryParse(test, out groupID);
                }

                if (notCondition)
                    return groupID != aniFile.GroupID;
                else
                    return groupID == aniFile.GroupID;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test if the file is manually linked
        /// No test parameter is required
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        private static bool EvaluateTestM(string test, SVR_AniDB_File aniFile, List<AniDB_Episode> episodes)
        {
            try
            {
                bool notCondition = false;
                if (!string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!"))
                    notCondition = true;

                // for a file to be manually linked it must NOT have an anifile, but does need episodes attached
                bool manuallyLinked = false;
                if (aniFile == null)
                {
                    manuallyLinked = true;
                    if (episodes == null || episodes.Count == 0)
                        manuallyLinked = false;
                }

                if (notCondition)
                    return !manuallyLinked;
                else
                    return manuallyLinked;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test if the file has any episodes linked
        /// No test parameter is required
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        private static bool EvaluateTestN(string test, SVR_AniDB_File aniFile, List<AniDB_Episode> episodes)
        {
            try
            {
                bool notCondition = false;
                if (!string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!"))
                    notCondition = true;

                bool epsLinked = false;
                if (aniFile == null)
                {
                    if (episodes != null && episodes.Count > 0)
                        epsLinked = true;
                }
                else
                    epsLinked = true;

                if (notCondition)
                    return !epsLinked;
                else
                    return epsLinked;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test if this file has the specified Dub (audio) language
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        private static bool EvaluateTestD(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                if (aniFile == null) return false;

                if (notCondition)
                {
                    foreach (Language lan in aniFile.Languages)
                    {
                        if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            return false;
                    }
                    return true;
                }
                else
                {
                    foreach (Language lan in aniFile.Languages)
                    {
                        if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test is this files has the specified Sub (subtitle) language
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <returns></returns>
        private static bool EvaluateTestS(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                if (aniFile == null) return false;

                if (
                    test.Trim().Equals(Constants.FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) &&
                    vid.GetAniDBFile().Subtitles.Count == 0)
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }

                if (notCondition)
                {
                    foreach (Language lan in aniFile.Subtitles)
                    {
                        if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            return false;
                    }
                    return true;
                }
                else
                {
                    foreach (Language lan in aniFile.Subtitles)
                    {
                        if (lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestF(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                if (aniFile == null) return false;

                int version = 0;
                int.TryParse(test, out version);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (aniFile.FileVersion == version) return true;
                        else return false;
                    }
                    else
                    {
                        if (aniFile.FileVersion == version) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (aniFile.FileVersion > version) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (aniFile.FileVersion >= version) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (aniFile.FileVersion < version) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (aniFile.FileVersion <= version) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestZ(string test, SVR_VideoLocal vid)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                int testBitDepth = 0;
                int.TryParse(test, out testBitDepth);

                int vidBitDepth = 0;
                int.TryParse(vid.VideoBitDepth, out vidBitDepth);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (testBitDepth == vidBitDepth) return true;
                        else return false;
                    }
                    else
                    {
                        if (testBitDepth == vidBitDepth) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (vidBitDepth > testBitDepth) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (vidBitDepth >= testBitDepth) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (vidBitDepth < testBitDepth) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (vidBitDepth <= testBitDepth) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestW(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                if (vid == null) return false;

                int testWidth = 0;
                int.TryParse(test, out testWidth);

                int width = 0;

                if (aniFile != null)
                    width = Utils.GetVideoWidth(aniFile.File_VideoResolution);

                if (width == 0)
                    width = Utils.GetVideoWidth(vid.VideoResolution);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (testWidth == width) return true;
                        else return false;
                    }
                    else
                    {
                        if (testWidth == width) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (width > testWidth) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (width >= testWidth) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (width < testWidth) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (width <= testWidth) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestU(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                if (vid == null) return false;

                int testHeight = 0;
                int.TryParse(test, out testHeight);

                int height = 0;
                if (aniFile != null)
                    height = Utils.GetVideoHeight(aniFile.File_VideoResolution);

                if (height == 0)
                    height = Utils.GetVideoHeight(vid.VideoResolution);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (testHeight == height) return true;
                        else return false;
                    }
                    else
                    {
                        if (testHeight == height) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (height > testHeight) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (height >= testHeight) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (height < testHeight) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (height <= testHeight) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }


        private static bool EvaluateTestR(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                if (aniFile == null) return false;

                bool hasSource = !string.IsNullOrEmpty(aniFile.File_Source);
                if (
                    test.Trim()
                        .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                    !hasSource)
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }


                if (test.Trim().Equals(aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (notCondition)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestC(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                if (aniFile == null) return false;

                // Video codecs
                bool hasSource = !string.IsNullOrEmpty(aniFile.File_VideoCodec);
                if (
                    test.Trim()
                        .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                    !hasSource)
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }


                if (test.Trim().Equals(aniFile.File_VideoCodec, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (notCondition)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestJ(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                if (aniFile == null) return false;

                // Audio codecs
                bool hasSource = !string.IsNullOrEmpty(aniFile.File_AudioCodec);
                if (
                    test.Trim()
                        .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                    !hasSource)
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }


                if (test.Trim().Equals(aniFile.File_AudioCodec, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (notCondition)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestT(string test, SVR_VideoLocal vid, SVR_AniDB_Anime anime)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                bool hasType = !string.IsNullOrEmpty(anime.GetAnimeTypeRAW());
                if (
                    test.Trim()
                        .Equals(Constants.FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                    !hasType)
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }


                if (test.Trim().Equals(anime.GetAnimeTypeRAW(), StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (notCondition)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestY(string test, SVR_VideoLocal vid, SVR_AniDB_Anime anime)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                int testYear = 0;
                int.TryParse(test, out testYear);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (anime.BeginYear == testYear) return true;
                        else return false;
                    }
                    else
                    {
                        if (anime.BeginYear == testYear) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (anime.BeginYear > testYear) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (anime.BeginYear >= testYear) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (anime.BeginYear < testYear) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (anime.BeginYear <= testYear) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestE(string test, SVR_VideoLocal vid, List<AniDB_Episode> episodes)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                int testEpNumber = 0;
                int.TryParse(test, out testEpNumber);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (episodes[0].EpisodeNumber == testEpNumber) return true;
                        else return false;
                    }
                    else
                    {
                        if (episodes[0].EpisodeNumber == testEpNumber) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (episodes[0].EpisodeNumber > testEpNumber) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (episodes[0].EpisodeNumber >= testEpNumber) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (episodes[0].EpisodeNumber < testEpNumber) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (episodes[0].EpisodeNumber <= testEpNumber) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        private static bool EvaluateTestH(string test, SVR_VideoLocal vid, List<AniDB_Episode> episodes)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }

                string epType = "";
                switch (episodes[0].GetEpisodeTypeEnum())
                {
                    case enEpisodeType.Episode:
                        epType = "E";
                        break;
                    case enEpisodeType.Credits:
                        epType = "C";
                        break;
                    case enEpisodeType.Other:
                        epType = "O";
                        break;
                    case enEpisodeType.Parody:
                        epType = "P";
                        break;
                    case enEpisodeType.Special:
                        epType = "S";
                        break;
                    case enEpisodeType.Trailer:
                        epType = "T";
                        break;
                }


                if (test.Trim().Equals(epType, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (notCondition)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
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
        private static void ProcessNumericalOperators(ref string test, ref bool notCondition, ref bool greaterThan,
            ref bool greaterThanEqual,
            ref bool lessThan, ref bool lessThanEqual)
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
            if (test.Substring(0, 1).Equals("<"))
            {
                lessThan = true;
                test = test.Substring(1, test.Length - 1);
                if (test.Substring(0, 1).Equals("="))
                {
                    lessThan = false;
                    lessThanEqual = true;
                    test = test.Substring(1, test.Length - 1);
                }
            }
        }

        private static bool EvaluateTestX(string test, SVR_VideoLocal vid, SVR_AniDB_Anime anime)
        {
            try
            {
                bool notCondition = false;
                bool greaterThan = false;
                bool greaterThanEqual = false;
                bool lessThan = false;
                bool lessThanEqual = false;

                ProcessNumericalOperators(ref test, ref notCondition, ref greaterThan, ref greaterThanEqual,
                    ref lessThan, ref lessThanEqual);

                int epCount = 0;
                int.TryParse(test, out epCount);

                bool hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

                if (!hasFileVersionOperator)
                {
                    if (!notCondition)
                    {
                        if (anime.EpisodeCountNormal == epCount) return true;
                        else return false;
                    }
                    else
                    {
                        if (anime.EpisodeCountNormal == epCount) return false;
                        else return true;
                    }
                }
                else
                {
                    if (greaterThan)
                    {
                        if (anime.EpisodeCountNormal > epCount) return true;
                        else return false;
                    }

                    if (greaterThanEqual)
                    {
                        if (anime.EpisodeCountNormal >= epCount) return true;
                        else return false;
                    }

                    if (lessThan)
                    {
                        if (anime.EpisodeCountNormal < epCount) return true;
                        else return false;
                    }

                    if (lessThanEqual)
                    {
                        if (anime.EpisodeCountNormal <= epCount) return true;
                        else return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Test whether the specified tag has a value
        /// </summary>
        /// <param name="test"></param>
        /// <param name="vid"></param>
        /// <param name="anime"></param>
        /// <returns></returns>
        private static bool EvaluateTestI(string test, SVR_VideoLocal vid, SVR_AniDB_File aniFile, List<AniDB_Episode> episodes,
            SVR_AniDB_Anime anime)
        {
            try
            {
                bool notCondition = false;
                if (test.Substring(0, 1).Equals("!"))
                {
                    notCondition = true;
                    test = test.Substring(1, test.Length - 1);
                }


                if (anime == null) return false;

                #region Test if Anime ID exists

                // Test if Anime ID exists

                string tagAnimeID = Constants.FileRenameTag.AnimeID.Substring(1,
                    Constants.FileRenameTag.AnimeID.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagAnimeID, StringComparison.InvariantCultureIgnoreCase))
                {
                    // manually linked files won't have an anime id
                    if (aniFile != null)
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                    else
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                }

                #endregion

                #region Test if Group ID exists

                // Test if Group ID exists

                string tagGroupID = Constants.FileRenameTag.GroupID.Substring(1,
                    Constants.FileRenameTag.GroupID.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagGroupID, StringComparison.InvariantCultureIgnoreCase))
                {
                    // manually linked files won't have an group id
                    if (aniFile != null)
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if Original File Name exists

                // Test if Original File Nameexists

                string tagOriginalFileName = Constants.FileRenameTag.OriginalFileName.Substring(1,
                    Constants.FileRenameTag.OriginalFileName.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagOriginalFileName, StringComparison.InvariantCultureIgnoreCase))
                {
                    // manually linked files won't have an Original File Name
                    if (aniFile != null)
                    {
                        if (string.IsNullOrEmpty(aniFile.FileName))
                        {
                            if (notCondition) return true;
                            else return false;
                        }
                        else
                        {
                            if (notCondition) return false;
                            else return true;
                        }
                    }
                    else
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                }

                #endregion

                #region Test if Episode Number exists

                // Test if Episode Number exists
                string tagEpisodeNumber = Constants.FileRenameTag.EpisodeNumber.Substring(1,
                    Constants.FileRenameTag.EpisodeNumber.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagEpisodeNumber, StringComparison.InvariantCultureIgnoreCase))
                {
                    // manually linked files won't have an Episode Number
                    if (aniFile != null)
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                    else
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                }

                #endregion

                #region Test file version

                // Test if Group Short Name exists - yes it always does
                string tagFileVersion = Constants.FileRenameTag.FileVersion.Substring(1,
                    Constants.FileRenameTag.FileVersion.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagFileVersion, StringComparison.InvariantCultureIgnoreCase))
                {
                    // manually linked files won't have an anime id
                    if (aniFile != null)
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                    else
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                }

                #endregion

                #region Test if ED2K Upper exists

                // Test if Group Short Name exists - yes it always does
                string tagED2KUpper = Constants.FileRenameTag.ED2KUpper.Substring(1,
                    Constants.FileRenameTag.ED2KUpper.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagED2KUpper, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition) return false;
                    else return true;
                }

                #endregion

                #region Test if ED2K Lower exists

                // Test if Group Short Name exists - yes it always does
                string tagED2KLower = Constants.FileRenameTag.ED2KLower.Substring(1,
                    Constants.FileRenameTag.ED2KLower.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagED2KLower, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (notCondition) return false;
                    else return true;
                }

                #endregion

                #region Test if English title exists

                string tagAnimeNameEnglish = Constants.FileRenameTag.AnimeNameEnglish.Substring(1,
                    Constants.FileRenameTag.AnimeNameEnglish.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagAnimeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (AniDB_Anime_Title ti in anime.GetTitles())
                    {
                        if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.English,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Official,
                                        StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (notCondition) return false;
                                else return true;
                            }
                        }
                    }

                    if (notCondition) return true;
                    else return false;
                }

                #endregion

                #region Test if Kanji title exists

                string tagAnimeNameKanji = Constants.FileRenameTag.AnimeNameKanji.Substring(1,
                    Constants.FileRenameTag.AnimeNameKanji.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagAnimeNameKanji, StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (AniDB_Anime_Title ti in anime.GetTitles())
                    {
                        if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.Kanji,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Official,
                                        StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (notCondition) return false;
                                else return true;
                            }
                        }
                    }
                    if (notCondition) return true;
                    else return false;
                }

                #endregion

                #region Test if Romaji title exists

                string tagAnimeNameRomaji = Constants.FileRenameTag.AnimeNameRomaji.Substring(1,
                    Constants.FileRenameTag.AnimeNameRomaji.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagAnimeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (AniDB_Anime_Title ti in anime.GetTitles())
                    {
                        if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.Romaji,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                                ti.TitleType.Trim()
                                    .Equals(Shoko.Models.Constants.AnimeTitleType.Official,
                                        StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (notCondition) return false;
                                else return true;
                            }
                        }
                    }
                    if (notCondition) return true;
                    else return false;
                }

                #endregion

                #region Test if episode name (english) exists

                string tagEpisodeNameEnglish = Constants.FileRenameTag.EpisodeNameEnglish.Substring(1,
                    Constants.FileRenameTag.EpisodeNameEnglish.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagEpisodeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(episodes[0].EnglishName))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if episode name (romaji) exists

                string tagEpisodeNameRomaji = Constants.FileRenameTag.EpisodeNameRomaji.Substring(1,
                    Constants.FileRenameTag.EpisodeNameRomaji.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagEpisodeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(episodes[0].RomajiName))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if group name short exists

                // Test if Group Short Name exists - yes it always does
                string tagGroupShortName = Constants.FileRenameTag.GroupShortName.Substring(1,
                    Constants.FileRenameTag.GroupShortName.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagGroupShortName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || string.IsNullOrEmpty(aniFile.Anime_GroupNameShort))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if group name long exists

                // Test if Group Short Name exists - yes it always does
                string tagGroupLongName = Constants.FileRenameTag.GroupLongName.Substring(1,
                    Constants.FileRenameTag.GroupLongName.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagGroupLongName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || string.IsNullOrEmpty(aniFile.Anime_GroupName))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if CRC Lower exists

                // Test if Group Short Name exists - yes it always does
                string tagCRCLower = Constants.FileRenameTag.CRCLower.Substring(1,
                    Constants.FileRenameTag.CRCLower.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagCRCLower, StringComparison.InvariantCultureIgnoreCase))
                {
                    string crc = vid.CRC32;
                    if (string.IsNullOrEmpty(crc) && aniFile != null)
                        crc = aniFile.CRC;

                    if (string.IsNullOrEmpty(crc))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if CRC Upper exists

                // Test if Group Short Name exists - yes it always does
                string tagCRCUpper = Constants.FileRenameTag.CRCUpper.Substring(1,
                    Constants.FileRenameTag.CRCUpper.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagCRCUpper, StringComparison.InvariantCultureIgnoreCase))
                {
                    string crc = vid.CRC32;
                    if (string.IsNullOrEmpty(crc) && aniFile != null)
                        crc = aniFile.CRC;

                    if (string.IsNullOrEmpty(crc))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test file has an audio track

                string tagDubLanguage = Constants.FileRenameTag.DubLanguage.Substring(1,
                    Constants.FileRenameTag.DubLanguage.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagDubLanguage, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || aniFile.Languages.Count == 0)
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test file has a subtitle track

                string tagSubLanguage = Constants.FileRenameTag.SubLanguage.Substring(1,
                    Constants.FileRenameTag.SubLanguage.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagSubLanguage, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || aniFile.Subtitles.Count == 0)
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if Video resolution exists

                string tagVidRes = Constants.FileRenameTag.Resolution.Substring(1,
                    Constants.FileRenameTag.Resolution.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagVidRes, StringComparison.InvariantCultureIgnoreCase))
                {
                    string vidRes = "";
                    if (aniFile != null)
                        vidRes = aniFile.File_VideoResolution;

                    if (string.IsNullOrEmpty(vidRes) && vid != null)
                        vidRes = vid.VideoResolution;

                    if (string.IsNullOrEmpty(vidRes))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test file has a video codec defined

                string tagVideoCodec = Constants.FileRenameTag.VideoCodec.Substring(1,
                    Constants.FileRenameTag.VideoCodec.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagVideoCodec, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || string.IsNullOrEmpty(aniFile.File_VideoCodec))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test file has an audio codec defined

                string tagAudioCodec = Constants.FileRenameTag.AudioCodec.Substring(1,
                    Constants.FileRenameTag.AudioCodec.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagAudioCodec, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (aniFile == null || string.IsNullOrEmpty(aniFile.File_AudioCodec))
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test file has Video Bit Depth defined

                string tagVideoBitDepth = Constants.FileRenameTag.VideoBitDepth.Substring(1,
                    Constants.FileRenameTag.VideoBitDepth.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagVideoBitDepth, StringComparison.InvariantCultureIgnoreCase))
                {
                    bool bitDepthExists = false;
                    if (vid != null)
                    {
                        if (!string.IsNullOrEmpty(vid.VideoBitDepth))
                        {
                            int bitDepth = 0;
                            int.TryParse(vid.VideoBitDepth, out bitDepth);
                            if (bitDepth > 0) bitDepthExists = true;
                        }
                    }
                    if (!bitDepthExists)
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if censored

                string tagCensored = Constants.FileRenameTag.Censored.Substring(1,
                    Constants.FileRenameTag.Censored.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagCensored, StringComparison.InvariantCultureIgnoreCase))
                {
                    bool isCensored = false;
                    if (aniFile != null)
                        isCensored = aniFile.IsCensored == 1;

                    if (!isCensored)
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if Deprecated

                string tagDeprecated = Constants.FileRenameTag.Deprecated.Substring(1,
                    Constants.FileRenameTag.Deprecated.Length - 1); // remove % at the front
                if (test.Trim().Equals(tagDeprecated, StringComparison.InvariantCultureIgnoreCase))
                {
                    bool isDeprecated = false;
                    if (aniFile != null)
                        isDeprecated = aniFile.IsDeprecated == 1;

                    if (!isDeprecated)
                    {
                        if (notCondition) return true;
                        else return false;
                    }
                    else
                    {
                        if (notCondition) return false;
                        else return true;
                    }
                }

                #endregion

                #region Test if file has more than one episode

                #endregion

                return false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        public static void Test(SVR_VideoLocal vid)
        {
            /*string testScript = "IF A(69),A(1),A(2) DO FAIL" + Environment.NewLine + //Do not rename file if it is Naruto
				"DO ADD '%eng (%ann) - %enr - %epn '" + Environment.NewLine + //Add the base, same for all files
				"IF D(japanese);S(english) DO ADD '(SUB)'" + Environment.NewLine + //Add (SUB) if the file is subbed in english
				"IF D(japanese);S(none) DO ADD '(RAW)'" + Environment.NewLine + //Add (RAW) if the file is not subbed.
				"IF G(!unknown) DO ADD '[%grp]'" + Environment.NewLine + //Add group name if it is not unknown
				"DO ADD '(%CRC)'" + Environment.NewLine; //Always add crc
			*/
            //this would create the schema "%eng (%ann) - %enr - %epn (SUB)[%grp](%CRC)" for a normal subbed file.


            string testScript = "IF A(69),A(1),A(2) DO FAIL" + Environment.NewLine +
                                //Do not rename file if it is Naruto
                                //// Anime Name
                                "IF I(eng) DO ADD '%eng - '" + Environment.NewLine +
                                // if the anime has an official/main title english add it to the string
                                "IF I(ann);I(!eng) DO ADD '%ann - '" + Environment.NewLine +
                                //If the anime has a romaji title but not an english title add the romaji anime title
                                //// Episode Number
                                "DO ADD '%enr - '" + Environment.NewLine + //Add the base, same for all files
                                //// Episode Title
                                "IF I(epr) DO ADD '%epr'" + Environment.NewLine +
                                //If the episode has an english title add to string
                                "IF I(epn);I(!epr) DO ADD '%epn'" + Environment.NewLine +
                                //If the episode has an romaji title but not an english title add the romaji episode title
                                "IF D(japanese);S(english) DO ADD '(SUB)'" + Environment.NewLine +
                                //Add (SUB) if the file is subbed in english
                                "IF D(japanese);S(none) DO ADD '(RAW)'" + Environment.NewLine +
                                //Add (RAW) if the file is not subbed.
                                "IF G(!unknown) DO ADD '[%grp]'" + Environment.NewLine +
                                //Add group name if it is not unknown
                                "DO ADD '(%CRC)'" + Environment.NewLine; //Always add crc

            string newName = GetNewFileName(vid, testScript);
            Debug.WriteLine(newName);
        }

        public static string GetNewFileName(SVR_VideoLocal vid, string script)
        {
            string[] lines = script.Split(Environment.NewLine.ToCharArray());

            string newFileName = string.Empty;


            List<AniDB_Episode> episodes = new List<AniDB_Episode>();
            SVR_AniDB_Anime anime = null;

            if (vid == null) return string.Empty;

            // get all the data so we don't need to get multiple times
            SVR_AniDB_File aniFile = vid.GetAniDBFile();
            if (aniFile == null)
            {
                List<SVR_AnimeEpisode> animeEps = vid.GetAnimeEpisodes();
                if (animeEps.Count == 0) return string.Empty;

                episodes.Add(animeEps[0].AniDB_Episode);

                anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
                if (anime == null) return string.Empty;
            }
            else
            {
                episodes = aniFile.Episodes;
                if (episodes.Count == 0) return string.Empty;

                anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
                if (anime == null) return string.Empty;
            }

            foreach (string line in lines)
            {
                string thisLine = line.Trim();
                if (thisLine.Length == 0) continue;

                // remove all comments from this line
                int comPos = thisLine.IndexOf("//");
                if (comPos >= 0)
                {
                    thisLine = thisLine.Substring(0, comPos);
                }


                // check if this line has no tests (applied to all files)
                if (thisLine.StartsWith(Constants.FileRenameReserved.Do, StringComparison.InvariantCultureIgnoreCase))
                {
                    string action = GetAction(thisLine);
                    PerformActionOnFileName(ref newFileName, action, vid, aniFile, episodes, anime);
                }
                else
                {
                    try
                    {
                        if (EvaluateTest(thisLine, vid, aniFile, episodes, anime))
                        {
                            Debug.WriteLine(string.Format("Line passed: {0}", thisLine));
                            // if the line has passed the tests, then perform the action

                            string action = GetAction(thisLine);

                            // if the action is fail, we don't want to rename
                            if (action.ToUpper()
                                .Trim()
                                .Equals(Constants.FileRenameReserved.Fail, StringComparison.InvariantCultureIgnoreCase))
                                return string.Empty;

                            PerformActionOnFileName(ref newFileName, action, vid, aniFile, episodes, anime);
                        }
                        else
                        {
                            Debug.WriteLine(string.Format("Line failed: {0}", thisLine));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }

            if (string.IsNullOrEmpty(newFileName)) return string.Empty;

            // finally add back the extension

            return string.Format("{0}{1}", newFileName.Replace("`", "'"), Path.GetExtension(vid.FileName));
        }

        private static void PerformActionOnFileName(ref string newFileName, string action, SVR_VideoLocal vid,
            SVR_AniDB_File aniFile, List<AniDB_Episode> episodes, SVR_AniDB_Anime anime)
        {
            // find the first test
            int posStart = action.IndexOf(" ");
            if (posStart < 0) return;

            string actionType = action.Substring(0, posStart);
            string parameter = action.Substring(posStart + 1, action.Length - posStart - 1);

            try
            {
                // action is to add the the new file name
                if (actionType.Trim()
                    .Equals(Constants.FileRenameReserved.Add, StringComparison.InvariantCultureIgnoreCase))
                    PerformActionOnFileNameADD(ref newFileName, parameter, vid, aniFile, episodes, anime);

                if (actionType.Trim()
                    .Equals(Constants.FileRenameReserved.Replace, StringComparison.InvariantCultureIgnoreCase))
                    PerformActionOnFileNameREPLACE(ref newFileName, parameter, vid, aniFile, episodes, anime);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static void PerformActionOnFileNameREPLACE(ref string newFileName, string action, SVR_VideoLocal vid,
            SVR_AniDB_File aniFile, List<AniDB_Episode> episodes, SVR_AniDB_Anime anime)
        {
            try
            {
                action = action.Trim();

                int posStart1 = action.IndexOf("'", 0);
                if (posStart1 < 0) return;

                int posEnd1 = action.IndexOf("'", posStart1 + 1);
                if (posEnd1 < 0) return;

                string toReplace = action.Substring(posStart1 + 1, posEnd1 - posStart1 - 1);

                int posStart2 = action.IndexOf("'", posEnd1 + 1);
                if (posStart2 < 0) return;

                int posEnd2 = action.IndexOf("'", posStart2 + 1);
                if (posEnd2 < 0) return;

                string replaceWith = action.Substring(posStart2 + 1, posEnd2 - posStart2 - 1);

                newFileName = newFileName.Replace(toReplace, replaceWith);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        private static void PerformActionOnFileNameADD(ref string newFileName, string action, SVR_VideoLocal vid,
            SVR_AniDB_File aniFile, List<AniDB_Episode> episodes, SVR_AniDB_Anime anime)
        {
            // TODO Remove illegal characters
            // TODO check for double episodes
            // TODO allow for synonyms to be used
            // TODO allow a strategy for episode numbers
            //      fixed 0 padding, look at number of episodes in series


            newFileName += action;
            newFileName = newFileName.Replace("'", "");

            #region Anime ID

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeID.ToLower()))
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeID, anime.AnimeID.ToString());
            }

            #endregion

            #region English title

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameEnglish.ToLower()))
            {
                foreach (AniDB_Anime_Title ti in anime.GetTitles())
                {
                    if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.English,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
                        {
                            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameEnglish, ti.Title);
                        }
                    }
                }
            }

            #endregion

            #region Romaji title

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameRomaji.ToLower()))
            {
                foreach (AniDB_Anime_Title ti in anime.GetTitles())
                {
                    if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.Romaji,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
                        {
                            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameRomaji, ti.Title);
                        }
                    }
                }
            }

            #endregion

            #region Kanji title

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.AnimeNameKanji.ToLower()))
            {
                foreach (AniDB_Anime_Title ti in anime.GetTitles())
                {
                    if (ti.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.Kanji,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Main, StringComparison.InvariantCultureIgnoreCase) ||
                            ti.TitleType.Trim()
                                .Equals(Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
                        {
                            newFileName = newFileName.Replace(Constants.FileRenameTag.AnimeNameKanji, ti.Title);
                        }
                    }
                }
            }

            #endregion

            #region Episode Number

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNumber.ToLower()))
            {
                int zeroPadding = 2;
                string prefix = "";

                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Credits) prefix = "C";
                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Other) prefix = "O";
                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Parody) prefix = "P";
                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Special) prefix = "S";
                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Trailer) prefix = "T";

                int epCount = 1;

                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Episode) epCount = anime.EpisodeCountNormal;
                if (episodes[0].GetEpisodeTypeEnum() == enEpisodeType.Special) epCount = anime.EpisodeCountSpecial;

                if (epCount > 10 && epCount < 100) zeroPadding = 2;
                if (epCount > 99 && epCount < 1000) zeroPadding = 3;
                if (epCount > 999) zeroPadding = 4;

                string episodeNumber = "";

                // normal episode
                episodeNumber = prefix + episodes[0].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');

                if (episodes.Count > 1)
                    episodeNumber += "-" +
                                     episodes[episodes.Count - 1].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');

                newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNumber, episodeNumber);
            }

            #endregion

            #region Episode name (english)

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNameEnglish.ToLower()))
                newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameEnglish, episodes[0].EnglishName);

            #endregion

            #region Episode name (romaji)

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.EpisodeNameRomaji.ToLower()))
                newFileName = newFileName.Replace(Constants.FileRenameTag.EpisodeNameRomaji, episodes[0].RomajiName);

            #endregion

            #region sub group name (short)

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupShortName.ToLower()))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.GroupShortName,
                        aniFile.Anime_GroupNameShort);
            }

            #endregion

            #region sub group name (long)

            if (action.Trim().ToLower().Contains(Constants.FileRenameTag.GroupLongName.ToLower()))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.GroupLongName, aniFile.Anime_GroupName);
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
                string crc = vid.CRC32;
                if (string.IsNullOrEmpty(crc) && aniFile != null)
                    crc = aniFile.CRC;

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
                string crc = vid.CRC32;
                if (string.IsNullOrEmpty(crc) && aniFile != null)
                    crc = aniFile.CRC;

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
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.FileVersion,
                        aniFile.FileVersion.ToString());
            }

            #endregion

            #region Audio languages (dub)

            if (action.Trim().Contains(Constants.FileRenameTag.DubLanguage))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.DubLanguage, aniFile.LanguagesRAW);
            }

            #endregion

            #region Subtitle languages (sub)

            if (action.Trim().Contains(Constants.FileRenameTag.SubLanguage))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.SubLanguage, aniFile.SubtitlesRAW);
            }

            #endregion

            #region Video Codec

            if (action.Trim().Contains(Constants.FileRenameTag.VideoCodec))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.VideoCodec, aniFile.File_VideoCodec);
            }

            #endregion

            #region Audio Codec

            if (action.Trim().Contains(Constants.FileRenameTag.AudioCodec))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.AudioCodec, aniFile.File_AudioCodec);
            }

            #endregion

            #region Video Bit Depth

            if (action.Trim().Contains(Constants.FileRenameTag.VideoBitDepth))
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.VideoBitDepth, vid.VideoBitDepth);
            }

            #endregion

            #region Video Source

            if (action.Trim().Contains(Constants.FileRenameTag.Source))
            {
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.Source, aniFile.File_Source);
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
                string res = "";
                bool hasResolution = true;
                if (aniFile != null)
                {
                    res = aniFile.File_VideoResolution;
                    if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
                        hasResolution = false;
                    if (aniFile.File_VideoResolution.Equals(Constants.FileRenameReserved.Unknown,
                        StringComparison.InvariantCultureIgnoreCase)) hasResolution = false;
                }
                else
                    hasResolution = false;

                if (!hasResolution)
                {
                    // try the video info
                    if (vid != null) res = vid.VideoResolution;
                }

                newFileName = newFileName.Replace(Constants.FileRenameTag.Resolution, res.Trim());
            }

            #endregion

	        #region Video Height

	        if (action.Trim().Contains(Constants.FileRenameTag.VideoHeight))
	        {
		        string res = "";
		        bool hasResolution = true;
		        if (aniFile != null)
		        {
			        res = aniFile.File_VideoResolution;
			        if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
				        hasResolution = false;
			        if (aniFile.File_VideoResolution.Equals(Constants.FileRenameReserved.Unknown,
				        StringComparison.InvariantCultureIgnoreCase)) hasResolution = false;
		        }
		        else
			        hasResolution = false;

		        if (!hasResolution)
		        {
			        // try the video info
			        if (vid != null) res = vid.VideoResolution;
		        }
		        res = res.Trim();
		        if (res.Length > 0) res = res.Split('x')[0];

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
                    newFileName = newFileName.Replace(Constants.FileRenameTag.FileID, aniFile.FileID.ToString());
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
                if (aniFile != null)
                    newFileName = newFileName.Replace(Constants.FileRenameTag.GroupID, aniFile.GroupID.ToString());
            }

            #endregion

            #region Original File Name

            if (action.Trim().Contains(Constants.FileRenameTag.OriginalFileName))
            {
                // remove the extension first 
                if (aniFile != null)
                {
                    string ext = Path.GetExtension(aniFile.FileName);
                    string partial = aniFile.FileName.Substring(0, aniFile.FileName.Length - ext.Length);

                    newFileName = newFileName.Replace(Constants.FileRenameTag.OriginalFileName, partial);
                }
            }

            #endregion

            #region Censored

            if (action.Trim().Contains(Constants.FileRenameTag.Censored))
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.Censored, "cen");
            }

            #endregion

            #region Deprecated

            if (action.Trim().Contains(Constants.FileRenameTag.Deprecated))
            {
                newFileName = newFileName.Replace(Constants.FileRenameTag.Deprecated, "depr");
            }

            #endregion
        }

        private static string GetAction(string line)
        {
            // find the first test
            int posStart = line.IndexOf("DO ");
            if (posStart < 0) return "";

            string action = line.Substring(posStart + 3, line.Length - posStart - 3);
            return action;
        }

        private static bool EvaluateTest(string line, SVR_VideoLocal vid, SVR_AniDB_File aniFile, List<AniDB_Episode> episodes,
            SVR_AniDB_Anime anime)
        {
            line = line.Trim();
            // determine if this line has a test
            foreach (char c in validTests)
            {
                string prefix = string.Format("IF {0}(", c);
                if (line.ToUpper().StartsWith(prefix))
                {
                    // find the first test
                    int posStart = line.IndexOf('(');
                    int posEnd = line.IndexOf(')');
                    int posStartOrig = posStart;

                    if (posEnd < posStart) return false;

                    string condition = line.Substring(posStart + 1, posEnd - posStart - 1);
                    bool passed = EvaluateTest(c, condition, vid, aniFile, episodes, anime);

                    // check for OR's and AND's
                    bool foundAND = false;
                    while (posStart > 0)
                    {
                        posStart = line.IndexOf(';', posStart);
                        if (posStart > 0)
                        {
                            foundAND = true;
                            string thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                                // remove any spacing
                            //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                            char thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                            int posStartNew = thisLineRemainder.IndexOf('(');
                            int posEndNew = thisLineRemainder.IndexOf(')');
                            condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                            bool thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

                            if (!passed || !thisPassed) return false;

                            posStart = posStart + 1;
                        }
                    }

                    // if the first test passed, and we only have OR's left then it is an automatic success
                    if (passed) return true;

                    if (!foundAND)
                    {
                        posStart = posStartOrig;
                        while (posStart > 0)
                        {
                            posStart = line.IndexOf(',', posStart);
                            if (posStart > 0)
                            {
                                string thisLineRemainder =
                                    line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                                    // remove any spacing
                                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                                char thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                                int posStartNew = thisLineRemainder.IndexOf('(');
                                int posEndNew = thisLineRemainder.IndexOf(')');
                                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                                bool thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

                                if (thisPassed) return true;

                                posStart = posStart + 1;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool EvaluateTest(char testChar, string testCondition, SVR_VideoLocal vid, SVR_AniDB_File aniFile,
            List<AniDB_Episode> episodes, SVR_AniDB_Anime anime)
        {
            testCondition = testCondition.Trim();

            switch (testChar)
            {
                case 'A':
                    return EvaluateTestA(testCondition, vid, aniFile, episodes);
                case 'G':
                    return EvaluateTestG(testCondition, vid, aniFile);
                case 'D':
                    return EvaluateTestD(testCondition, vid, aniFile);
                case 'S':
                    return EvaluateTestS(testCondition, vid, aniFile);
                case 'F':
                    return EvaluateTestF(testCondition, vid, aniFile);
                case 'R':
                    return EvaluateTestR(testCondition, vid, aniFile);
                case 'Z':
                    return EvaluateTestZ(testCondition, vid);
                case 'T':
                    return EvaluateTestT(testCondition, vid, anime);
                case 'Y':
                    return EvaluateTestY(testCondition, vid, anime);
                case 'E':
                    return EvaluateTestE(testCondition, vid, episodes);
                case 'H':
                    return EvaluateTestH(testCondition, vid, episodes);
                case 'X':
                    return EvaluateTestX(testCondition, vid, anime);
                case 'C':
                    return EvaluateTestC(testCondition, vid, aniFile);
                case 'J':
                    return EvaluateTestJ(testCondition, vid, aniFile);
                case 'I':
                    return EvaluateTestI(testCondition, vid, aniFile, episodes, anime);
                case 'W':
                    return EvaluateTestW(testCondition, vid, aniFile);
                case 'U':
                    return EvaluateTestU(testCondition, vid, aniFile);
                case 'M':
                    return EvaluateTestM(testCondition, aniFile, episodes);
                case 'N':
                    return EvaluateTestN(testCondition, aniFile, episodes);
            }

            return false;
        }

        public static string GetNewFileName(SVR_VideoLocal vid)
        {
            try
            {

                RenameScript defaultScript = RepoFactory.RenameScript.GetDefaultScript();

                if (defaultScript == null) return string.Empty;

                return GetNewFileName(vid, defaultScript.ScriptName);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return string.Empty;
            }
        }
    }
}