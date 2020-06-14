using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Models.Enums;

namespace Shoko.Models
{
    public class FileQualityPreferences
    {
        [JsonIgnore]
        public static readonly Dictionary<string, string> SimplifiedSources;

        static FileQualityPreferences()
        {
            SimplifiedSources = new Dictionary<string, string>
            {
                {"unknown", "unknown"},
                {"raw", "unknown"},
                {"tv", "tv"},
                {"dtv", "tv"},
                {"hdtv", "tv"},
                {"dvd", "dvd"},
                {"hkdvd", "dvd"},
                {"hddvd", "dvd"},
                {"bd", "bd"},
                {"blu-ray", "bd"},
                {"bluray", "bd"},
                {"ld", "ld"},
                {"web", "www"},
                {"www", "www"}
            };
        }

        /// This is a list, in order, of the operations to compare. Accepts no arguments.
        public List<FileQualityFilterType> PreferredTypes { get; set; } = new List<FileQualityFilterType>
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.RESOLUTION, FileQualityFilterType.AUDIOSTREAMCOUNT,
            FileQualityFilterType.SUBSTREAMCOUNT, FileQualityFilterType.SUBGROUP, FileQualityFilterType.CHAPTER,
            FileQualityFilterType.VIDEOCODEC, FileQualityFilterType.AUDIOCODEC, FileQualityFilterType.VERSION
        };

        /// Preferred Audio Codecs, in order.
        public List<string> PreferredAudioCodecs { get; set; } = new List<string>
        {
            "flac", "dca", "aac", "ac3", "wmav2", "wmapro", "adpcm_ms", "mp3", "mp2", "vp6f"
        };

        /// Preferred Resolutions, in order.
        public List<string> PreferredResolutions { get; set; } = new List<string> {"2160p", "1440p", "1080p", "720p", "480p"};

        /// Preferred Sources, in order.
        [JsonIgnore] public List<string> _sources = new List<string> {"bd", "dvd", "tv", "www", "unknown"};

        /// Subbing/Release Groups in order of preference.
        /// If a file is not in the list, the compared files are considered comparatively equal.
        public List<string> PreferredSubGroups { get; set; } = new List<string> { "fffpeeps", "doki", "commie", "horriblesubs" };

        /// Preferred Video Codecs, in order.
        /// Due to the complexity of Bit Depth, it has its own setting that is used when applicable.
        public List<string> PreferredVideoCodecs { get; set; } = new List<string>
        {
            "hevc", "h264", "mpeg4", "vc1", "flv", "mpeg2", "mpeg1", "vp6f"
        };

        /// Whether or not to prefer Bit Depth. If/when 12bit becomes more prominant, this can be changed to an integer.
        /// This will not apply to codecs that don't support.
        public bool Prefer8BitVideo { get; set; }

        /// <summary>
        /// Allow deletion of the file being imported.
        /// Main use case is if you set your client to dl everything and let Shoko pick the best
        /// If ServerSettings.FileQualityFilterEnabled is not true, this does nothing
        /// </summary>
        public bool AllowDeletionOfImportedFiles { get; set; }

        /// These are used to determine whether or not a file that isn't found to be the most preferred is kept.
        /// Each Setting must have an operation type, except for Types
        /// All checks must pass to keep a file.
        /// This is a list, in order, of the operations. Accepts no arguments.
        public List<FileQualityFilterType> RequiredTypes { get; set; } = new List<FileQualityFilterType>
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.CHAPTER, FileQualityFilterType.VERSION
        };

        /// Required Audio Codec. Default must be FLAC, Dolby, or AAC.
        /// Accepts IN, NOTIN
        public FileQualityTypeListPair<List<string>> RequiredAudioCodecs =
            new FileQualityTypeListPair<List<string>>(
                new List<string> {"flac", "dca", "aac"},
                FileQualityFilterOperationType.IN);

        /// Required Audio Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        public FileQualityTypeListPair<int> RequiredAudioStreamCount =
            new FileQualityTypeListPair<int>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Resolution. Default must be 1080p or greater.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ, IN, NOTIN
        public FileQualityTypeListPair<List<string>> RequiredResolutions =
            new FileQualityTypeListPair<List<string>>(new List<string> {"1080p"},
                FileQualityFilterOperationType.GREATER_EQ);

        /// Required Source. Default must be BD or DVD release.
        /// Accepts IN, NOTIN
        public FileQualityTypeListPair<List<string>> RequiredSources =
            new FileQualityTypeListPair<List<string>>(new List<string> {"bd", "dvd"},
                FileQualityFilterOperationType.IN);

        /// The required Subbing/Release Groups and the operator. Defaulting to not HorribleSubs for example.
        /// Accepts IN, NOTIN
        public FileQualityTypeListPair<List<string>> RequiredSubGroups =
            new FileQualityTypeListPair<List<string>>(new List<string> { "horriblesubs" }, FileQualityFilterOperationType.NOTIN);

        /// Required Subtitle Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        public FileQualityTypeListPair<int> RequiredSubStreamCount =
            new FileQualityTypeListPair<int>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Video Codec. Default must be H265/HEVC or H264/AVC.
        /// Accepts IN, NOTIN
        public FileQualityTypeListPair<List<string>> RequiredVideoCodecs { get; set; } =
            new FileQualityTypeListPair<List<string>>(new List<string> {"hevc", "h264"},
                FileQualityFilterOperationType.IN);

        /// Require 10bit Video when applicable. This will not apply to codecs that don't support.
        public bool Require10BitVideo = true;

        /// The maximum number of files to keep per episode.
        /// I'll need to think of a way to handle episodes that are multipart, but listed as one episode.
        /// For now, just make sure to mark said files as variations, and it will not be deleted regardless.
        public int MaxNumberOfFilesToKeep = 1;

        /// Preferred Sources, in order.
        public List<string> PreferredSources
        {
            get => _sources;
            set => _sources = value.Where(a => !string.IsNullOrEmpty(a)).Select(a =>
            {
                string source = a.ToLowerInvariant();
                if (SimplifiedSources.ContainsKey(source)) source = SimplifiedSources[source];
                return source;
            }).ToList();
        }

        public class FileQualityTypeListPair<T>
        {
            public FileQualityFilterOperationType Operator { get; set; }
            public T Value { get; set; }
            
            public FileQualityTypeListPair() { }
            
            public FileQualityTypeListPair(T value, FileQualityFilterOperationType type)
            {
                Value = value;
                Operator = type;
            }
        }
    }
}
