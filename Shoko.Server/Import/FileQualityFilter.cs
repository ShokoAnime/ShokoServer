using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
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

        public enum FileQualityFilterType
        {
            RESOLUTION,
            SOURCE,
            VERSION,
            AUDIOSTREAMCOUNT,
            VIDEOCODEC,
            AUDIOCODEC,
            CHAPTER,
            SUBGROUP,
            SUBSTREAMCOUNT
        }

        public static FileQualityFilterType[] TypesPreferences =
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.RESOLUTION, FileQualityFilterType.AUDIOSTREAMCOUNT,
            FileQualityFilterType.SUBSTREAMCOUNT, FileQualityFilterType.SUBGROUP, FileQualityFilterType.CHAPTER,
            FileQualityFilterType.VIDEOCODEC, FileQualityFilterType.AUDIOCODEC, FileQualityFilterType.VERSION,
        };

        private static string[] _subgroups = { "fffpeeps", "doki", "commie", "horriblesubs" };
        public static List<string> SubGoupPreferences
        {
            get => _subgroups.ToList();
            set => _subgroups = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }

        public static string[] QualityPreferences = {"bd", "dvd", "hdtv", "tv", "www", "unknown"};

        public static string[] AudioCodecPreferences =
        {
            "flac", "dolby digital plus", "dolby truehd", "dts", "he-aac", "aac", "ac3", "mp3 cbr", "mp3 vbr",
            "vorbis (ogg vorbis)", "wma (also divx audio)", "realaudio g2/8 (cook)", "alac", "msaudio", "opus", "mp2",
            "pcm", "unknown", "none"
        };

        public static string[] VideoCodecPreferences =
        {
            "hevc", "h264/avc", "divx5/6", "mpeg-4 asp", "mpeg-2", "mpeg-1", "ms mp4x", "divx4", "divx3", "vc-1",
            "xvid", "realvideo 9/10", "other (divx)", "other (mpeg-4 sp)", "other (realvideo)", "vp8", "vp6",
            "other (vpx)", "other (wmv3/wmv/9)", "unknown", "none"
        };
        public static bool Prefer8BitVideo = false;

        // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
        public static int CompareTo(this SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            var oldEp = oldFile.GetAniDBFile();
            var newEp = newFile.GetAniDBFile();
            if (newEp == null) return 1;
            if (oldEp == null) return -1;
            int result = 0;

            foreach (FileQualityFilterType type in TypesPreferences)
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
                        result = CompareQualityTo(newEp, oldEp);
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

        private static int CompareResolutionTo(AniDB_File newFile, AniDB_File oldFile)
        {
            string[] res = oldFile.File_VideoResolution.Split('x');
            if (res.Length != 2) return 0;
            int oldWidth = 0;
            int oldHeight = 0;
            if (!int.TryParse(res[0], out oldWidth)) return 0;
            if (!int.TryParse(res[1], out oldHeight)) return 0;
            res = newFile.File_VideoResolution.Split('x');
            if (res.Length != 2) return 0;
            int newWidth = 0;
            int newHeight = 0;
            if (!int.TryParse(res[0], out newWidth)) return 0;
            if (!int.TryParse(res[1], out newHeight)) return 0;
            if (newWidth * newHeight > oldWidth * oldHeight) return -1;
            if (newWidth * newHeight < oldWidth * oldHeight) return 1;

            return 0;
        }

        private static int CompareAudioCodecTo(AniDB_File newFile, AniDB_File oldFile)
        {
            string[] newCodecs = newFile.File_AudioCodec.ToLowerInvariant().Split('\'');
            string[] oldCodecs = oldFile.File_AudioCodec.ToLowerInvariant().Split('\'');
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(AudioCodecPreferences, newCodec);
                int oldIndex = Array.IndexOf(AudioCodecPreferences, oldCodec);
                if (newIndex < 0 || oldIndex < 0) continue;
                int result = newIndex.CompareTo(oldIndex);
                if (result != 0) return result;
            }
            return 0;
        }

        private static int CompareVideoCodecTo(SVR_VideoLocal newLocal, AniDB_File newFile, SVR_VideoLocal oldLocal, AniDB_File oldFile)
        {
            string[] newCodecs = newFile.File_VideoCodec.ToLowerInvariant().Split('\'');
            string[] oldCodecs = oldFile.File_VideoCodec.ToLowerInvariant().Split('\'');
            // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
            if (newCodecs.Length != oldCodecs.Length) return 0;

            for (int i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
            {
                string newCodec = newCodecs[i];
                string oldCodec = oldCodecs[i];
                int newIndex = Array.IndexOf(VideoCodecPreferences, newCodec);
                int oldIndex = Array.IndexOf(VideoCodecPreferences, oldCodec);
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

        private static int CompareAudioStreamCountTo(AniDB_File newFile, AniDB_File oldFile)
        {
            int newStreamCount = newFile.File_AudioCodec.Split('\'').Length;
            int oldStreamCount = oldFile.File_AudioCodec.Split('\'').Length;
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

        private static int CompareChapterTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
        {
            return 0;
        }

        private static int CompareQualityTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (string.IsNullOrEmpty(newFile.File_Source) || string.IsNullOrEmpty(oldFile.File_Source)) return 0;
            int newIndex = Array.IndexOf(QualityPreferences, newFile.File_Source);
            int oldIndex = Array.IndexOf(QualityPreferences, oldFile.File_Source);
            return newIndex.CompareTo(oldIndex);
        }

        private static int CompareSubGroupTo(AniDB_File newFile, AniDB_File oldFile)
        {
            if (!_subgroups.Contains(newFile.Anime_GroupName.ToLowerInvariant())) return 0;
            if (_subgroups.Contains(newFile.Anime_GroupName.ToLowerInvariant()) &&
                !_subgroups.Contains(oldFile.Anime_GroupName.ToLowerInvariant())) return 0;
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

    }
}