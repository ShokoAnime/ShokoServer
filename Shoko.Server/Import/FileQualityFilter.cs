using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Settings;

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
        public static FileQualityPreferences Settings => ServerSettings.Instance.FileQualityFilterPreferences;

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
            string[] codecs =
                aniFile?.Media?.AudioStreams.Select(a => LegacyMediaUtils.TranslateCodec(a.Codec)).OrderBy(a => a)
                    .ToArray() ?? new string[] { };
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
            int streamCount = aniFile?.Media?.AudioStreams.Count ?? -1;
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
            return aniFile?.GetAniDBFile()?.IsChaptered == 1 || (aniFile?.Media?.MenuStreams.Any() ?? false);
        }

        private static bool CheckDeprecated(AniDB_File aniFile)
        {
            return aniFile?.IsDeprecated == 0;
        }

        private static bool CheckResolution(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile)
        {
            Tuple<int, int> resTuple = GetResolutionInternal(videoLocal, aniFile);
            string res = MediaInfoUtils.GetStandardResolution(resTuple);
            if (res == null) return true;

            int resArea = resTuple.Item1 * resTuple.Item2;

            FileQualityFilterOperationType operationType = Settings.RequiredResolutionOperator;
            switch (operationType)
            {
                case FileQualityFilterOperationType.EQUALS:
                    return res.Equals(Settings.RequiredResolutions.FirstOrDefault());
                case FileQualityFilterOperationType.GREATER_EQ:
                    List<int> keysGT = MediaInfoUtils.ResolutionArea.Keys.Where(a => resArea >= a).ToList();
                    keysGT.AddRange(MediaInfoUtils.ResolutionArea43.Keys.Where(a => resArea >= a));
                    List<string> valuesGT = new List<string>();
                    foreach (int key in keysGT)
                    {
                        if (MediaInfoUtils.ResolutionArea.ContainsKey(key)) valuesGT.Add(MediaInfoUtils.ResolutionArea[key]);
                        if (MediaInfoUtils.ResolutionArea43.ContainsKey(key)) valuesGT.Add(MediaInfoUtils.ResolutionArea43[key]);
                    }

                    if (valuesGT.FindInEnumerable(Settings.RequiredResolutions)) return true;
                    break;
                case FileQualityFilterOperationType.LESS_EQ:
                    List<int> keysLT = MediaInfoUtils.ResolutionArea.Keys.Where(a => resArea <= a).ToList();
                    keysLT.AddRange(MediaInfoUtils.ResolutionArea43.Keys.Where(a => resArea <= a));
                    List<string> valuesLT = new List<string>();
                    foreach (int key in keysLT)
                    {
                        if (MediaInfoUtils.ResolutionArea.ContainsKey(key)) valuesLT.Add(MediaInfoUtils.ResolutionArea[key]);
                        if (MediaInfoUtils.ResolutionArea43.ContainsKey(key)) valuesLT.Add(MediaInfoUtils.ResolutionArea43[key]);
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
            if (IsNullOrUnknown(aniFile)) return false;
            FileQualityFilterOperationType operationType = Settings.RequiredSourceOperator;
            var source = aniFile.File_Source.ToLowerInvariant();
            if (FileQualityPreferences.SimplifiedSources.ContainsKey(source))
                source = FileQualityPreferences.SimplifiedSources[source];
            switch (operationType)
            {
                case FileQualityFilterOperationType.IN:
                    return Settings._requiredsources.Item1.Contains(source);
                case FileQualityFilterOperationType.NOTIN:
                    return !Settings._requiredsources.Item1.Contains(source);
            }

            return true;
        }

        private static bool CheckSubGroup(AniDB_File aniFile)
        {
            if (IsNullOrUnknown(aniFile)) return false;
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
            int streamCount = file?.Media?.TextStreams.Count ?? -1;
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
            string[] codecs =
                aniFile?.Media?.media.track.Where(a => a.type == StreamType.Video)
                    .Select(a => LegacyMediaUtils.TranslateCodec(a.Codec))
                    .OrderBy(a => a).ToArray() ?? new string[] { };

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
                        if (IsNullOrUnknown(newEp) && IsNullOrUnknown(oldEp)) return 0;
                        if (IsNullOrUnknown(newEp)) return 1;
                        if (IsNullOrUnknown(oldEp)) return -1;
                        result = CompareSourceTo(newEp, oldEp);
                        break;

                    case FileQualityFilterType.SUBGROUP:
                        if (IsNullOrUnknown(newEp) && IsNullOrUnknown(oldEp)) return 0;
                        if (IsNullOrUnknown(newEp)) return 1;
                        if (IsNullOrUnknown(oldEp)) return -1;
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
            string[] newCodecs = newFile?.Media?.AudioStreams.Select(a =>
                {
                    string codec = a.Codec ?? a.CodecID;
                    var trans = LegacyMediaUtils.TranslateCodec(codec);
                    if (trans == null) return codec?.ToLowerInvariant();
                    return trans;
                }).Where(a => a != null).OrderBy(a => a)
                .ToArray() ?? new string[] { };
            string[] oldCodecs = oldFile?.Media?.AudioStreams.Select(a =>
                {
                    string codec = a.Codec ?? a.CodecID;
                    var trans = LegacyMediaUtils.TranslateCodec(codec);
                    if (trans == null) return codec?.ToLowerInvariant();
                    return trans;
                }).Where(a => a != null).OrderBy(a => a)
                .ToArray() ?? new string[] { };
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
            int newStreamCount = newFile.Media?.AudioStreams.Count ?? 0;
            int oldStreamCount = oldFile.Media?.AudioStreams.Count ?? 0;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareChapterTo(SVR_VideoLocal newFile, AniDB_File newAniFile, SVR_VideoLocal oldFile,
            AniDB_File oldAniFile)
        {
            if ((newAniFile?.IsChaptered == 1 || newFile.Media.MenuStreams.Any()) &&
                !(oldAniFile?.IsChaptered == 1 || oldFile.Media.MenuStreams.Any())) return -1;
            if (!(newAniFile?.IsChaptered == 1 || newFile.Media.MenuStreams.Any()) &&
                (oldAniFile?.IsChaptered == 1 || oldFile.Media.MenuStreams.Any())) return 1;
            return (oldAniFile?.IsChaptered == 1 || oldFile.Media.MenuStreams.Any()).CompareTo(
                newAniFile?.IsChaptered == 1 || newFile.Media.MenuStreams.Any());
        }

        private static int CompareResolutionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile,
            SVR_AniDB_File newAniFile, SVR_AniDB_File oldAniFile)
        {
            string oldRes = GetResolution(oldFile, oldAniFile);
            string newRes = GetResolution(newFile, newAniFile);

            if (newRes == null && oldRes == null) return 0;
            if (newRes == null) return 1;
            if (oldRes == null) return -1;

            string[] res = Settings._resolutions.ToArray();
            if (!res.Contains(newRes) && !res.Contains(oldRes)) return 0;
            if (!res.Contains(newRes)) return 1;
            if (!res.Contains(oldRes)) return -1;

            int newIndex = Array.IndexOf(res, newRes);
            int oldIndex = Array.IndexOf(res, oldRes);
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSourceTo(AniDB_File newFile, AniDB_File oldFile)
        {
            var newSource = newFile.File_Source.ToLowerInvariant();
            if (FileQualityPreferences.SimplifiedSources.ContainsKey(newSource))
                newSource = FileQualityPreferences.SimplifiedSources[newSource];
            var oldSource = oldFile.File_Source.ToLowerInvariant();
            if (FileQualityPreferences.SimplifiedSources.ContainsKey(oldSource))
                oldSource = FileQualityPreferences.SimplifiedSources[oldSource];
            int newIndex = Array.IndexOf(Settings._sources, newSource);
            int oldIndex = Array.IndexOf(Settings._sources, oldSource);
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
            int newStreamCount = newFile?.Media?.TextStreams.Count ?? 0;
            int oldStreamCount = oldFile?.Media?.TextStreams.Count ?? 0;
            return oldStreamCount.CompareTo(newStreamCount);
        }

        private static int CompareVersionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            AniDB_File newAni = newFile?.GetAniDBFile();
            AniDB_File oldAni = oldFile?.GetAniDBFile();
            if (newAni == null || oldAni == null) return 0;
            if (!newAni.Anime_GroupName.Equals(oldAni.Anime_GroupName)) return 0;
            if (!newAni.File_VideoResolution.Equals(oldAni.File_VideoResolution)) return 0;
            if (!(newFile.Media?.VideoStream?.BitDepth).Equals(oldFile.Media?.VideoStream?.BitDepth)) return 0;
            if (!string.Equals(newFile.Media?.VideoStream?.CodecID, oldFile.Media?.VideoStream?.CodecID)) return 0;
            return oldAni.FileVersion.CompareTo(newAni.FileVersion);
        }

        private static int CompareVideoCodecTo(SVR_VideoLocal newLocal, SVR_VideoLocal oldLocal)
        {
            string[] newCodecs =
                newLocal?.Media?.media.track.Where(a => a.type == StreamType.Video)
                    .Select(a =>
                    {
                        string codec = a.Codec ?? a.CodecID;
                        var trans = LegacyMediaUtils.TranslateCodec(codec);
                        if (trans == null) return codec?.ToLowerInvariant();
                        return trans;
                    }).Where(a => a != null).OrderBy(a => a).ToArray() ??
                new string[] { };
            string[] oldCodecs =
                oldLocal?.Media?.media.track.Where(a => a.type == StreamType.Video)
                    .Select(a =>
                    {
                        string codec = a.Codec ?? a.CodecID;
                        var trans = LegacyMediaUtils.TranslateCodec(codec);
                        if (trans == null) return codec?.ToLowerInvariant();
                        return trans;
                    }).Where(a => a != null).OrderBy(a => a).ToArray() ??
                new string[] { };
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
                if (newLocal?.Media?.VideoStream?.BitDepth == null ||
                    oldLocal?.Media?.VideoStream?.BitDepth == null) continue;
                if (newLocal.Media.VideoStream.BitDepth == 8 && oldLocal.Media.VideoStream.BitDepth == 10)
                        return Settings.Prefer8BitVideo ? -1 : 1;
                if (newLocal.Media.VideoStream.BitDepth == 10 && oldLocal.Media.VideoStream.BitDepth == 8)
                    return Settings.Prefer8BitVideo ? 1 : -1;
            }

            return 0;
        }

        #endregion

        #region Information from Models (Operations that aren't simple)

        public static string GetResolution(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile = null)
        {
            if (aniFile == null)
                aniFile = videoLocal?.GetAniDBFile();
            return MediaInfoUtils.GetStandardResolution(GetResolutionInternal(videoLocal, aniFile));
        }

        public static string GetResolution(string res)
        {
            if (string.IsNullOrEmpty(res)) return null;
            string[] parts = res.Split('x');
            if (parts.Length != 2) return null;
            if (!int.TryParse(parts[0], out int width)) return null;
            if (!int.TryParse(parts[1], out int height)) return null;
            return MediaInfoUtils.GetStandardResolution(new Tuple<int, int>(width, height));
        }

        private static Tuple<int, int> GetResolutionInternal(SVR_VideoLocal videoLocal, SVR_AniDB_File aniFile)
        {
            string[] res = aniFile?.File_VideoResolution?.Split('x');
            int oldHeight = 0, oldWidth = 0;
            if (res != null && res.Length == 2 && res[0] == "0" && res[1] == "0")
            {
                int.TryParse(res[0], out oldWidth);
                int.TryParse(res[1], out oldHeight);
            }

            if (oldHeight == 0 || oldWidth == 0)
            {
                var stream = videoLocal?.Media?.VideoStream;
                if (stream != null)
                {
                    oldWidth = stream.Width;
                    oldHeight = stream.Height;
                }
            }

            if (oldHeight == 0 || oldWidth == 0) return null;
            return new Tuple<int, int>(oldWidth, oldHeight);
        }

        public static bool IsNullOrUnknown(AniDB_File file)
        {
            if (file == null) return true;
            if (string.IsNullOrWhiteSpace(file.File_Source)) return true;
            if (string.IsNullOrWhiteSpace(file.Anime_GroupName)) return true;
            if (string.IsNullOrWhiteSpace(file.Anime_GroupNameShort)) return true;
            if (file.Anime_GroupName.EqualsInvariantIgnoreCase("unknown")) return true;
            if (file.Anime_GroupNameShort.EqualsInvariantIgnoreCase("unknown")) return true;
            if (file.Anime_GroupName.EqualsInvariantIgnoreCase("raw")) return true;
            if (file.Anime_GroupNameShort.EqualsInvariantIgnoreCase("raw")) return true;
            if (file.File_Source.EqualsInvariantIgnoreCase("unknown")) return true;
            if (file.File_Source.EqualsInvariantIgnoreCase("raw")) return true;
            return false;
        }
        #endregion
    }
}
