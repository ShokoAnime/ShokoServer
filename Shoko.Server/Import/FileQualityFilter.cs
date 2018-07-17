using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

namespace Shoko.Server
{
    public static class FileQualityFilter
    {
        /*
        Types (This is to determine the order of these types to use)
            List

        Quality -- AniDB_File.File_Source
            can be Array, but will prolly use List
        - BD
        - DVD
        - HDTV
        - TV
        - www
        - unknown

        Resolution (use rounding to determine where strange sizes fit)
            can be array, but will prolly use List
        - > 1080p
        - 1080p (BD)
        - 720p (BD Downscale and TV)
        - 540p (Nice DVD)
        - 480p (DVD)
        - < 480p (I really don't care at this low)

        Sub Groups (Need searching, will use fuzzy)
            List (Ordered set technically)
        ex.
        - Doki
        - ...
        - HorribleSubs

        Not configurable
        Higher version from the same release group, source, and resolution
        Chaptered over not chaptered

        make an enum
        reference said enum through a CompareByType

        */

        public static readonly Dictionary<int, string> ResolutionArea;
        public static readonly Dictionary<int, string> ResolutionAreaOld;

        public static FileQualityPreferences Settings = new FileQualityPreferences();

        static FileQualityFilter()
        {
            ResolutionArea = new Dictionary<int, string>
            {
                {3840 * 2160, "2160p"},
                {2560 * 1440, "1440p"},
                {1920 * 1080, "1080p"},
                {1280 * 720, "720p"},
                {1024 * 576, "576p"},
                {853 * 480, "480p"}
            };

            ResolutionAreaOld = new Dictionary<int, string>
            {
                {720 * 576, "576p"},
                {720 * 480, "480p"},
                {480 * 360, "360p"},
                {320 * 240, "240p"}
            };
        }

        #region Checks
        public static bool CheckFileKeep(SVR_VideoLocal file)
        {
            bool result = true;

            SVR_AniDB_File aniFile = file?.GetAniDBFile();
            // Don't delete files with missing info. If it's not getting updated, then do it manually
            if (aniFile != null)
            {
                if (aniFile.File_Source.Equals("unknown")) return true;
                if (aniFile.File_VideoResolution.Equals("0x0")) return true;
            }
            foreach (var type in Settings._requiredtypes)
            {
                if (!result) break;
                switch (type)
                {
                    case FileQualityFilterType.AUDIOCODEC:
                        result &= CheckAudioCodec(file);
                        break;
                    case FileQualityFilterType.AUDIOSTREAMCOUNT:
                        result &= CheckAudioStreamCount(file);
                        break;
                    case FileQualityFilterType.CHAPTER:
                        if (aniFile == null) return false;
                        result &= CheckChaptered(file);
                        break;
                    case FileQualityFilterType.RESOLUTION:
                        result &= CheckResolution(file, aniFile);
                        break;
                    case FileQualityFilterType.SOURCE:
                        if (aniFile == null) return false;
                        result &= CheckSource(aniFile);
                        break;
                    case FileQualityFilterType.SUBGROUP:
                        if (aniFile == null) return false;
                        result &= CheckSubGroup(aniFile);
                        break;
                    case FileQualityFilterType.SUBSTREAMCOUNT:
                        result &= CheckSubStreamCount(file);
                        break;
                    case FileQualityFilterType.VERSION:
                        if (aniFile == null) return false;
                        result &= CheckDeprecated(aniFile);
                        break;
                    case FileQualityFilterType.VIDEOCODEC:
                        if (aniFile == null) return false;
                        result &= CheckVideoCodec(file);
                        break;
                }
            }

            return result;
        }

        private static bool CheckAudioCodec(SVR_VideoLocal aniFile)
        {
            string[] codecs = aniFile?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 2)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};
            if (codecs.Length == 0) return false;

            FileQualityFilterOperationType operationType = Settings.RequiredAudioCodecOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return codecs.FindInEnumerable(Settings._requiredaudiocodecs.Item1);
                case FileQualityFilterOperationType.NOTIN:
                    return !codecs.FindInEnumerable(Settings._requiredaudiocodecs.Item1);
            }
            return true;
        }

        private static bool CheckAudioStreamCount(SVR_VideoLocal aniFile)
        {
            int streamCount = aniFile?.Media?.Parts?.SelectMany(a => a.Streams).Count(a => a.StreamType == 2) ?? -1;
            if (streamCount == -1) return true;

            FileQualityFilterOperationType operationType = Settings.RequiredAudioStreamCountOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return streamCount == Settings.RequiredAudioStreamCount;
                case FileQualityFilterOperationType.GREATER_EQ:
                    return streamCount >= Settings.RequiredAudioStreamCount;
                case FileQualityFilterOperationType.LESS_EQ:
                    return streamCount <= Settings.RequiredAudioStreamCount;
            }
            return true;
        }

        private static bool CheckChaptered(SVR_VideoLocal aniFile)
        {
            return aniFile?.GetAniDBFile()?.IsChaptered == 1 || (aniFile?.Media?.Chaptered ?? false);
        }

        private static bool CheckDeprecated(AniDB_File aniFile)
        {
            return aniFile?.IsDeprecated == 0;
        }

        private static bool CheckResolution(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile)
        {
            Tuple<int, int> resTuple = GetResolutionInternal(videoLocal, aniFile);
            string res = GetResolution(resTuple);
            if (res == null) return false;

            int resArea = resTuple.Item1 * resTuple.Item2;

            FileQualityFilterOperationType operationType = Settings.RequiredResolutionOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return res.Equals(Settings.RequiredResolutions.FirstOrDefault());
                case FileQualityFilterOperationType.GREATER_EQ:
                    List<int> keysGT = ResolutionArea.Keys.Where(a => resArea >= a).ToList();
                    keysGT.AddRange(ResolutionAreaOld.Keys.Where(a => resArea >= a));
                    List<string> valuesGT = new List<string>();
                    foreach (int key in keysGT)
                    {
                        if (ResolutionArea.ContainsKey(key)) valuesGT.Add(ResolutionArea[key]);
                        if (ResolutionAreaOld.ContainsKey(key)) valuesGT.Add(ResolutionAreaOld[key]);
                    }
                    if (valuesGT.FindInEnumerable(Settings.RequiredResolutions)) return true;
                    break;
                case FileQualityFilterOperationType.LESS_EQ:
                    List<int> keysLT = ResolutionArea.Keys.Where(a => resArea <= a).ToList();
                    keysLT.AddRange(ResolutionAreaOld.Keys.Where(a => resArea <= a));
                    List<string> valuesLT = new List<string>();
                    foreach (int key in keysLT)
                    {
                        if (ResolutionArea.ContainsKey(key)) valuesLT.Add(ResolutionArea[key]);
                        if (ResolutionAreaOld.ContainsKey(key)) valuesLT.Add(ResolutionAreaOld[key]);
                    }
                    if (valuesLT.FindInEnumerable(Settings.RequiredResolutions)) return true;
                    break;
                case FileQualityFilterOperationType.IN:
                    return Settings.RequiredResolutions.Contains(res);
                case FileQualityFilterOperationType.NOTIN:
                    return !Settings.RequiredResolutions.Contains(res);
            }
            return false;
        }

        private static bool CheckSource(AniDB_File aniFile)
        {
            if (string.IsNullOrEmpty(aniFile?.File_Source)) return false;
            FileQualityFilterOperationType operationType = Settings.RequiredSourceOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return Settings._requiredsources.Item1.Contains(aniFile.File_Source.ToLowerInvariant());
                case FileQualityFilterOperationType.NOTIN:
                    return !Settings._requiredsources.Item1.Contains(aniFile.File_Source.ToLowerInvariant());
            }
            return true;
        }

        private static bool CheckSubGroup(AniDB_File aniFile)
        {
            if (aniFile == null) return false;
            FileQualityFilterOperationType operationType = Settings.RequiredSubGroupOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return Settings._requiredsubgroups.Item1.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) ||
                           Settings._requiredsubgroups.Item1.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant());
                case FileQualityFilterOperationType.NOTIN:
                    return !Settings._requiredsubgroups.Item1.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) &&
                           !Settings._requiredsubgroups.Item1.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant());
            }
            return true;
        }

        private static bool CheckSubStreamCount(SVR_VideoLocal file)
        {
            int streamCount = file?.Media?.Parts?.SelectMany(a => a.Streams).Count(b => b.StreamType == 3) ?? -1;
            if (streamCount == -1) return true;

            FileQualityFilterOperationType operationType = Settings.RequiredSubStreamCountOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return streamCount == Settings.RequiredSubStreamCount;
                case FileQualityFilterOperationType.GREATER_EQ:
                    return streamCount >= Settings.RequiredSubStreamCount;
                case FileQualityFilterOperationType.LESS_EQ:
                    return streamCount <= Settings.RequiredSubStreamCount;
            }
            return true;
        }

        private static bool CheckVideoCodec(SVR_VideoLocal aniFile)
        {
            string[] codecs = aniFile?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 1)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};

            if (codecs.Length == 0) return false;
            FileQualityFilterOperationType operationType = Settings.RequiredVideoCodecOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return Settings._requiredvideocodecs.Item1.FindInEnumerable(codecs);
                case FileQualityFilterOperationType.NOTIN:
                    return !Settings._requiredvideocodecs.Item1.FindInEnumerable(codecs);
            }
            return true;
        }

        #endregion

        #region Comparisons
        // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
        public static int CompareTo(this SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            var oldEp = oldFile?.GetAniDBFile();
            var newEp = newFile?.GetAniDBFile();
            int result = 0;

            foreach (FileQualityFilterType type in Settings._types)
            {
                switch (type)
                {
                    case FileQualityFilterType.AUDIOCODEC:
                        result = CompareAudioCodecTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.AUDIOSTREAMCOUNT:
                        result = CompareAudioStreamCountTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.CHAPTER:
                        result = CompareChapterTo(newFile, newEp, oldFile, oldEp);
                        break;

                    case FileQualityFilterType.RESOLUTION:
                        result = CompareResolutionTo(newFile, oldFile, newEp, oldEp);
                        break;

                    case FileQualityFilterType.SOURCE:
                        if (newEp == null) return 1;
                        if (oldEp == null) return -1;
                        result = CompareSourceTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SUBGROUP:
                        if (newEp == null) return 1;
                        if (oldEp == null) return -1;
                        result = CompareSubGroupTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SUBSTREAMCOUNT:
                        result = CompareSubStreamCountTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.VERSION:
                        if (newEp == null) return 1;
                        if (oldEp == null) return -1;
                        result = CompareVersionTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.VIDEOCODEC:
                        result = CompareVideoCodecTo(newFile, oldFile);
                        break;
                }
                if (result != 0) return result;
            }

            return 0;
        }

        private static int CompareAudioCodecTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            string[] newCodecs = newFile?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 2)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};
            string[] oldCodecs = oldFile?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 2)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(Settings._audiocodecs, newCodec);
                int oldIndex = Array.IndexOf(Settings._audiocodecs, oldCodec);
                if (newIndex < 0 || oldIndex < 0) continue;
                int result = newIndex.CompareTo(oldIndex);
                if (result != 0) return result;
            }
            return 0;
        }

        private static int CompareAudioStreamCountTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            int newStreamCount = newFile.Media?.Parts?.SelectMany(a => a.Streams).Count(a => a.StreamType == 2) ?? 0;
            int oldStreamCount = oldFile.Media?.Parts?.SelectMany(a => a.Streams).Count(a => a.StreamType == 2) ?? 0;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareChapterTo(SVR_VideoLocal newFile, AniDB_File newAniFile, SVR_VideoLocal oldFile, AniDB_File oldAniFile)
        {
            if ((newAniFile?.IsChaptered == 1 || newFile.Media.Chaptered) &&
                !(oldAniFile?.IsChaptered == 1 || oldFile.Media.Chaptered)) return -1;
            if (!(newAniFile?.IsChaptered == 1 || newFile.Media.Chaptered) &&
                (oldAniFile?.IsChaptered == 1 || oldFile.Media.Chaptered)) return 1;
            return (oldAniFile?.IsChaptered == 1 || oldFile.Media.Chaptered).CompareTo(
                newAniFile?.IsChaptered == 1 || newFile.Media.Chaptered);
        }

        private static int CompareResolutionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile, SVR_AniDB_File newAniFile, SVR_AniDB_File oldAniFile)
        {
            string oldRes = GetResolution(oldFile, oldAniFile);
            string newRes = GetResolution(newFile, newAniFile);

            if (newRes == null || oldRes == null) return 0;
            if (!Settings._resolutions.Contains(newRes)) return 0;
            if (!Settings._resolutions.Contains(oldRes)) return -1;
            int newIndex = Array.IndexOf(Settings._resolutions, newRes);
            int oldIndex = Array.IndexOf(Settings._resolutions, oldRes);
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSourceTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (string.IsNullOrEmpty(newFile.File_Source) || string.IsNullOrEmpty(oldFile.File_Source)) return 0;
            if (newFile.File_Source.Equals("unknown", StringComparison.InvariantCultureIgnoreCase) ||
                oldFile.File_Source.Equals("unknown", StringComparison.InvariantCultureIgnoreCase)) return 0;
            int newIndex = Array.IndexOf(Settings._sources, newFile.File_Source.ToLowerInvariant());
            int oldIndex = Array.IndexOf(Settings._sources, oldFile.File_Source.ToLowerInvariant());
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSubGroupTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (newFile == null || oldFile == null) return 0;
            if (!Settings._subgroups.Contains(newFile.Anime_GroupName.ToLowerInvariant()) &&
                !Settings._subgroups.Contains(newFile.Anime_GroupNameShort.ToLowerInvariant())) return 0;
            if (!Settings._subgroups.Contains(oldFile.Anime_GroupName.ToLowerInvariant()) &&
                !Settings._subgroups.Contains(oldFile.Anime_GroupNameShort.ToLowerInvariant())) return 0;
            // The above ensures that _subgroups contains both, so no need to check for -1 in this case
            int newIndex = Array.IndexOf(Settings._subgroups, newFile.Anime_GroupName.ToLowerInvariant());
            if (newIndex == -1)
                newIndex = Array.IndexOf(Settings._subgroups, newFile.Anime_GroupNameShort.ToLowerInvariant());

            int oldIndex = Array.IndexOf(Settings._subgroups, oldFile.Anime_GroupName.ToLowerInvariant());
            if (oldIndex == -1)
                oldIndex = Array.IndexOf(Settings._subgroups, oldFile.Anime_GroupNameShort.ToLowerInvariant());

            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSubStreamCountTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            int newStreamCount = newFile?.Media?.Parts?.Where(a => a.Streams.Any(b => b.StreamType == 3)).ToList().Count ?? 0;
            int oldStreamCount = oldFile?.Media?.Parts?.Where(a => a.Streams.Any(b => b.StreamType == 3)).ToList().Count ?? 0;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareVersionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            AniDB_File newAni = newFile?.GetAniDBFile();
            AniDB_File oldAni = oldFile?.GetAniDBFile();
            if (newAni == null || oldAni == null) return 0;
            if (!newAni.Anime_GroupName.Equals(oldAni.Anime_GroupName)) return 0;
            if (!newAni.File_VideoResolution.Equals(oldAni.File_VideoResolution)) return 0;
            if (!newFile.VideoBitDepth.Equals(oldFile.VideoBitDepth)) return 0;
            return oldAni.FileVersion.CompareTo(newAni.FileVersion);
        }

        private static int CompareVideoCodecTo(SVR_VideoLocal newLocal, SVR_VideoLocal oldLocal)
        {
            string[] newCodecs = newLocal?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 1)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};
            string[] oldCodecs = oldLocal?.Media?.Parts?.SelectMany(a => a.Streams)
                .Where(a => a.StreamType == 1)
                .Select(a => a.Codec)
                .OrderBy(a => a)
                .ToArray() ?? new string[] {};
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(Settings._videocodecs, newCodec);
                int oldIndex = Array.IndexOf(Settings._videocodecs, oldCodec);
                if (newIndex < 0 || oldIndex < 0) continue;
                int result = newIndex.CompareTo(oldIndex);
                if (result != 0) return result;
                if (string.IsNullOrEmpty(newLocal.VideoBitDepth) ||
                    string.IsNullOrEmpty(oldLocal.VideoBitDepth)) continue;
                if ((newLocal.VideoBitDepth.Equals("8") || newLocal.VideoBitDepth.Equals("10")) &&
                    (oldLocal.VideoBitDepth.Equals("8") || oldLocal.VideoBitDepth.Equals("10")))
                {
                    if (newLocal.VideoBitDepth.Equals("8") && oldLocal.VideoBitDepth.Equals("10"))
                        return Settings.Prefer8BitVideo ? -1 : 1;
                    if (newLocal.VideoBitDepth.Equals("10") && oldLocal.VideoBitDepth.Equals("8"))
                        return Settings.Prefer8BitVideo ? 1 : -1;
                }
            }
            return 0;
        }

        #endregion

        #region Information from Models (Operations that aren't simple)

        public static string GetResolution(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile)
        {
            return GetResolution(GetResolutionInternal(videoLocal, aniFile));
        }

        public static string GetResolution(string res)
        {
            if (string.IsNullOrEmpty(res)) return null;
            string[] parts = res.Split('x');
            if (parts.Length != 2) return null;
            if (!int.TryParse(parts[0], out int width)) return null;
            if (!int.TryParse(parts[1], out int height)) return null;
            return GetResolution(new Tuple<int, int>(width, height));
        }

        public static string GetResolution(Tuple<int, int> res)
        {
            if (res == null) return null;
            // not precise, but we are rounding and calculating distance anyway
            const double sixteenNine = 1.777778;
            const double fourThirds = 1.333333;
            double ratio = (double) res.Item1 / res.Item2;

            if ((ratio - sixteenNine) * (ratio - sixteenNine) < (ratio - fourThirds) * (ratio - fourThirds))
            {
                long area = res.Item1 * res.Item2;
                double keyDist = double.MaxValue;
                int key = 0;
                foreach (int resArea in ResolutionArea.Keys.ToList())
                {
                    double dist = Math.Sqrt((resArea - area) * (resArea - area));
                    if (dist < keyDist)
                    {
                        keyDist = dist;
                        key = resArea;
                    }
                }
                if (Math.Abs(keyDist - double.MaxValue) < 0.01D) return null;
                return ResolutionArea[key];
            }
            else
            {
                double area = res.Item1 * res.Item2;
                double keyDist = double.MaxValue;
                int key = 0;
                foreach (int resArea in ResolutionAreaOld.Keys.ToList())
                {
                    double dist = Math.Sqrt((resArea - area) * (resArea - area));
                    if (dist < keyDist)
                    {
                        keyDist = dist;
                        key = resArea;
                    }
                }
                if (Math.Abs(keyDist - long.MaxValue) < 0.01D) return null;
                return ResolutionAreaOld[key];
            }
        }

        private static Tuple<int, int> GetResolutionInternal(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile)
        {
            string[] res = aniFile?.File_VideoResolution?.Split('x');
            int oldHeight = 0, oldWidth = 0;
            if (res == null || res.Length != 2 || res[0] == "0" && res[1] == "0")
            {
                var stream = videoLocal?.Media?.Parts?.SelectMany(a => a.Streams)
                    .FirstOrDefault(a => a.StreamType == 1);
                if (stream != null)
                {
                    oldWidth = stream.Width;
                    oldHeight = stream.Height;
                }
            }
            if (res == null || res.Length != 2 || res[0] == "0" && res[1] == "0") return null;
            if (oldHeight == 0 && oldWidth == 0 && !int.TryParse(res[0], out oldWidth)) return null;
            if (oldHeight == 0 && oldWidth == 0 && !int.TryParse(res[1], out oldHeight)) return null;
            if (oldWidth == 0 || oldHeight == 0) return null;
            return new Tuple<int, int>(oldWidth, oldHeight);
        }
        #endregion
    }
}
