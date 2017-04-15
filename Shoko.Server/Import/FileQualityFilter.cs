using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using static Shoko.Server.FileQualityPreferences;
using static Shoko.Models.FileQualityFilter;

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

        public static Dictionary<int, string> ResolutionArea;
        public static Dictionary<int, string> ResolutionAreaOld;

        static FileQualityFilter()
        {
            ResolutionArea = new Dictionary<int, string>();
            ResolutionArea.Add(3840*2160, "2160p");
            ResolutionArea.Add(2560*1440, "1440p");
            ResolutionArea.Add(1920*1080, "1080p");
            ResolutionArea.Add(1280*720, "720p");
            ResolutionArea.Add(1024*576, "576p");
            ResolutionArea.Add(853*480, "480p");

            ResolutionAreaOld = new Dictionary<int, string>();
            ResolutionAreaOld.Add(720*576, "576p");
            ResolutionAreaOld.Add(720*480, "480p");
            ResolutionAreaOld.Add(480*360, "360p");
            ResolutionAreaOld.Add(320*240, "240p");
        }

        #region Checks
        public static bool CheckFileKeep(SVR_VideoLocal file)
        {
            bool result = true;
            List<FileQualityFilterType> requiredTypes = new List<FileQualityFilterType>();
            requiredTypes.Add(FileQualityFilterType.SOURCE);

            AniDB_File aniFile = file.GetAniDBFile();
            if (aniFile == null) return false;
            foreach (var type in requiredTypes)
            {
                if (!result) break;
                switch (type)
                {
                    case FileQualityFilterType.AUDIOCODEC:
                        result &= CheckAudioCodec(aniFile);
                        break;
                    case FileQualityFilterType.AUDIOSTREAMCOUNT:
                        result &= CheckAudioStreamCount(aniFile);
                        break;
                    case FileQualityFilterType.CHAPTER:
                        result &= CheckChaptered(file);
                        break;
                    case FileQualityFilterType.RESOLUTION:
                        result &= CheckResolution(aniFile);
                        break;
                    case FileQualityFilterType.SOURCE:
                        result &= CheckSource(aniFile);
                        break;
                    case FileQualityFilterType.SUBGROUP:
                        result &= CheckSubGroup(aniFile);
                        break;
                    case FileQualityFilterType.SUBSTREAMCOUNT:
                        result &= CheckSubStreamCount(file);
                        break;
                    case FileQualityFilterType.VERSION:
                        result &= CheckDeprecated(aniFile);
                        break;
                    case FileQualityFilterType.VIDEOCODEC:
                        result &= CheckVideoCodec(aniFile);
                        break;
                }
            }

            return result;
        }

        private static bool CheckAudioCodec(AniDB_File aniFile)
        {
            string[] codecs = aniFile.File_AudioCodec.ToLowerInvariant()
                .Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            if (codecs.Length == 0) return false;

            FileQualityFilterOperationType operationType = RequiredAudioCodecOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return codecs.FindInEnumerable(_requiredaudiocodecs.Item1);
                case FileQualityFilterOperationType.NOTIN:
                    return !codecs.FindInEnumerable(_requiredaudiocodecs.Item1);
            }
            return true;
        }

        private static bool CheckAudioStreamCount(AniDB_File aniFile)
        {
            int streamCount = aniFile.File_AudioCodec.ToLowerInvariant()
                .Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            FileQualityFilterOperationType operationType = RequiredAudioStreamCountOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return streamCount == RequiredAudioStreamCount;
                case FileQualityFilterOperationType.GREATER_EQ:
                    return streamCount >= RequiredAudioStreamCount;
                case FileQualityFilterOperationType.LESS_EQ:
                    return streamCount <= RequiredAudioStreamCount;
            }
            return true;
        }

        private static bool CheckChaptered(SVR_VideoLocal file)
        {

            return true;
        }

        private static bool CheckDeprecated(AniDB_File aniFile)
        {
            return aniFile.IsDeprecated == 0;
        }

        private static bool CheckResolution(AniDB_File aniFile)
        {
            Tuple<int, int> resTuple = GetResolutionInternal(aniFile);
            string res = GetResolution(resTuple);
            if (res == null) return false;

            int resArea = resTuple.Item1 * resTuple.Item2;

            FileQualityFilterOperationType operationType = RequiredResolutionOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return res.Equals(RequiredResolutions.FirstOrDefault());
                case FileQualityFilterOperationType.GREATER_EQ:
                    List<int> keysGT = ResolutionArea.Keys.Where(a => resArea >= a).ToList();
                    keysGT.AddRange(ResolutionAreaOld.Keys.Where(a => resArea >= a));
                    List<string> valuesGT = new List<string>();
                    foreach (int key in keysGT)
                    {
                        if (ResolutionArea.ContainsKey(key)) valuesGT.Add(ResolutionArea[key]);
                        if (ResolutionAreaOld.ContainsKey(key)) valuesGT.Add(ResolutionAreaOld[key]);
                    }
                    if (valuesGT.FindInEnumerable(RequiredResolutions)) return true;
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
                    if (valuesLT.FindInEnumerable(RequiredResolutions)) return true;
                    break;
                case FileQualityFilterOperationType.IN:
                    return RequiredResolutions.Contains(res);
                case FileQualityFilterOperationType.NOTIN:
                    return !RequiredResolutions.Contains(res);
            }
            return false;
        }

        private static bool CheckSource(AniDB_File aniFile)
        {
            if (string.IsNullOrEmpty(aniFile.File_Source)) return false;
            FileQualityFilterOperationType operationType = RequiredSourceOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return _requiredsources.Item1.Contains(aniFile.File_Source.ToLowerInvariant());
                case FileQualityFilterOperationType.NOTIN:
                    return !_requiredsources.Item1.Contains(aniFile.File_Source.ToLowerInvariant());
            }
            return true;
        }

        private static bool CheckSubGroup(AniDB_File aniFile)
        {
            FileQualityFilterOperationType operationType = RequiredSubGroupOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return _requiredsubgroups.Item1.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) ||
                           _requiredsubgroups.Item1.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant());
                case FileQualityFilterOperationType.NOTIN:
                    return !_requiredsubgroups.Item1.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) &&
                           !_requiredsubgroups.Item1.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant());
            }
            return true;
        }

        private static bool CheckSubStreamCount(SVR_VideoLocal file)
        {
            int streamCount = file.Media.Parts.Where(a => a.Streams.Any(b => b.StreamType == "3")).ToList().Count;
            FileQualityFilterOperationType operationType = RequiredSubStreamCountOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return streamCount == RequiredSubStreamCount;
                case FileQualityFilterOperationType.GREATER_EQ:
                    return streamCount >= RequiredSubStreamCount;
                case FileQualityFilterOperationType.LESS_EQ:
                    return streamCount <= RequiredSubStreamCount;
            }
            return true;
        }

        private static bool CheckVideoCodec(AniDB_File aniFile)
        {
            string[] codecs = aniFile.File_VideoCodec.ToLowerInvariant()
                .Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            if (codecs.Length == 0) return false;
            FileQualityFilterOperationType operationType = RequiredVideoCodecOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return _requiredvideocodecs.Item1.FindInEnumerable(codecs);
                case FileQualityFilterOperationType.NOTIN:
                    return !_requiredvideocodecs.Item1.FindInEnumerable(codecs);
            }
            return true;
        }

        #endregion

        #region Comparisons
        // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
        public static int CompareTo(this SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            var oldEp = oldFile.GetAniDBFile();
            var newEp = newFile.GetAniDBFile();
            if (newEp == null) return 1;
            if (oldEp == null) return -1;
            int result = 0;

            foreach (FileQualityFilterType type in _types)
            {
                switch (type)
                {
                    case FileQualityFilterType.AUDIOCODEC:
                        result = CompareAudioCodecTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.AUDIOSTREAMCOUNT:
                        result = CompareAudioStreamCountTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.CHAPTER:
                        result = CompareChapterTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.RESOLUTION:
                        result = CompareResolutionTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SOURCE:
                        result = CompareSourceTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SUBGROUP:
                        result = CompareSubGroupTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SUBSTREAMCOUNT:
                        result = CompareSubStreamCountTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.VERSION:
                        result = CompareVersionTo(newFile, oldFile);
                        break;

                    case FileQualityFilterType.VIDEOCODEC:
                        result = CompareVideoCodecTo(newFile, newEp, oldFile, oldEp);
                        break;
                }
                if (result != 0) return result;
            }

            return 0;
        }

        private static int CompareAudioCodecTo(AniDB_File newFile, AniDB_File oldFile)
        {
            string[] newCodecs = newFile.File_AudioCodec.ToLowerInvariant().Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            string[] oldCodecs = oldFile.File_AudioCodec.ToLowerInvariant().Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(_audiocodecs, newCodec);
                int oldIndex = Array.IndexOf(_audiocodecs, oldCodec);
                if (newIndex < 0 || oldIndex < 0) continue;
                int result = newIndex.CompareTo(oldIndex);
                if (result != 0) return result;
            }
            return 0;
        }

        private static int CompareAudioStreamCountTo(AniDB_File newFile, AniDB_File oldFile)
        {
            int newStreamCount = newFile.File_AudioCodec.Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries).Length;
            int oldStreamCount = oldFile.File_AudioCodec.Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries).Length;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareChapterTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            return 0;
        }

        private static int CompareResolutionTo(AniDB_File newFile, AniDB_File oldFile)
        {
            string oldRes = GetResolution(oldFile);
            string newRes = GetResolution(newFile);

            if (newRes == null || oldRes == null) return 0;
            if (!_resolutions.Contains(newRes)) return 0;
            if (!_resolutions.Contains(oldRes)) return -1;
            int newIndex = Array.IndexOf(_resolutions, newRes);
            int oldIndex = Array.IndexOf(_resolutions, oldRes);
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSourceTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (string.IsNullOrEmpty(newFile.File_Source) || string.IsNullOrEmpty(oldFile.File_Source)) return 0;
            int newIndex = Array.IndexOf(_sources, newFile.File_Source);
            int oldIndex = Array.IndexOf(_sources, oldFile.File_Source);
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSubGroupTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (!_subgroups.Contains(newFile.Anime_GroupName.ToLowerInvariant())) return 0;
            if (!_subgroups.Contains(oldFile.Anime_GroupName.ToLowerInvariant())) return 0;
            // The above ensures that _subgroups contains both, so no need to check for -1 in this case
            int newIndex = Array.IndexOf(_subgroups, newFile.Anime_GroupName.ToLowerInvariant());
            int oldIndex = Array.IndexOf(_subgroups, oldFile.Anime_GroupName.ToLowerInvariant());
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSubStreamCountTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            int newStreamCount = newFile.Media.Parts.Where(a => a.Streams.Any(b => b.StreamType == "3")).ToList().Count;
            int oldStreamCount = oldFile.Media.Parts.Where(a => a.Streams.Any(b => b.StreamType == "3")).ToList().Count;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareVersionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            AniDB_File newani = newFile.GetAniDBFile();
            AniDB_File oldani = oldFile.GetAniDBFile();
            if (!newani.Anime_GroupName.Equals(oldani.Anime_GroupName)) return 0;
            if (!newani.File_VideoResolution.Equals(oldani.File_VideoResolution)) return 0;
            if (!newFile.VideoBitDepth.Equals(oldFile.VideoBitDepth)) return 0;
            return oldani.FileVersion.CompareTo(newani.FileVersion);
        }

        private static int CompareVideoCodecTo(SVR_VideoLocal newLocal, AniDB_File newFile, SVR_VideoLocal oldLocal, AniDB_File oldFile)
        {
            string[] newCodecs = newFile.File_VideoCodec.ToLowerInvariant().Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            string[] oldCodecs = oldFile.File_VideoCodec.ToLowerInvariant().Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(_videocodecs, newCodec);
                int oldIndex = Array.IndexOf(_videocodecs, oldCodec);
                if (newIndex < 0 || oldIndex < 0) continue;
                int result = newIndex.CompareTo(oldIndex);
                if (result != 0) return result;
                if (string.IsNullOrEmpty(newLocal.VideoBitDepth) ||
                    string.IsNullOrEmpty(oldLocal.VideoBitDepth)) continue;
                if ((newLocal.VideoBitDepth.Equals("8") || newLocal.VideoBitDepth.Equals("10")) &&
                    (oldLocal.VideoBitDepth.Equals("8") || oldLocal.VideoBitDepth.Equals("10")))
                {
                    if (newLocal.VideoBitDepth.Equals("8") && oldLocal.VideoBitDepth.Equals("10"))
                        return Prefer8BitVideo ? -1 : 1;
                    if (newLocal.VideoBitDepth.Equals("10") && oldLocal.VideoBitDepth.Equals("8"))
                        return Prefer8BitVideo ? 1 : -1;
                }
            }
            return 0;
        }

        #endregion

        #region Information from Models (Operations that aren't simple)

        private static string GetResolution(AniDB_File aniFile)
        {
            return GetResolution(GetResolutionInternal(aniFile));
        }

        private static string GetResolution(Tuple<int, int> res)
        {
            if (res == null) return null;
            // not precise, but we are rounding and calculating distance anyway
            double sixteenNine = 1.777778;
            double fourThirds = 1.333333;
            double ratio = res.Item1 / res.Item2;

            if (Math.Abs(ratio - sixteenNine) < Math.Abs(ratio - fourThirds))
            {
                int area = res.Item1 * res.Item2;
                int key = int.MaxValue;
                foreach (int resArea in ResolutionArea.Keys.ToList())
                {
                    int dist = Math.Abs(resArea - area);
                    if (dist < key) key = dist;
                }
                if (key == int.MaxValue) return null;
                return ResolutionArea[key];
            }
            else
            {
                int area = res.Item1 * res.Item2;
                int key = int.MaxValue;
                foreach (int resArea in ResolutionAreaOld.Keys.ToList())
                {
                    int dist = Math.Abs(resArea - area);
                    if (dist < key) key = dist;
                }
                if (key == int.MaxValue) return null;
                return ResolutionAreaOld[key];
            }
        }

        private static Tuple<int, int> GetResolutionInternal(AniDB_File aniFile)
        {
            string[] res = aniFile.File_VideoResolution.Split('x');
            if (res.Length != 2) return null;
            int oldWidth = 0;
            int oldHeight = 0;
            if (!int.TryParse(res[0], out oldWidth)) return null;
            if (!int.TryParse(res[1], out oldHeight)) return null;
            if (oldWidth == 0 || oldHeight == 0) return null;
            return new Tuple<int, int>(oldWidth, oldHeight);
        }
        #endregion
    }
}