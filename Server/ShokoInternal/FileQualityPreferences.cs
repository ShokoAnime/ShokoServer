using System;
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
        [JsonProperty("Types")]
        public FileQualityFilterType[] _types =
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.RESOLUTION, FileQualityFilterType.AUDIOSTREAMCOUNT,
            FileQualityFilterType.SUBSTREAMCOUNT, FileQualityFilterType.SUBGROUP, FileQualityFilterType.CHAPTER,
            FileQualityFilterType.VIDEOCODEC, FileQualityFilterType.AUDIOCODEC, FileQualityFilterType.VERSION
        };

        /// Preferred Audio Codecs, in order.
        [JsonProperty("PreferredAudioCodecs")]
        public string[] _audiocodecs =
        {
            "flac", "dca", "aac", "ac3", "wmav2", "wmapro", "adpcm_ms", "mp3", "mp2", "vp6f"
        };

        /// Preferred Resolutions, in order.
        [JsonProperty("PreferredResolutions")]
        public string[] _resolutions = {"2160p", "1440p", "1080p", "720p", "480p"};

        /// Preferred Sources, in order.
        [JsonProperty("PreferredSources")]
        public string[] _sources = {"bd", "dvd", "tv", "www", "unknown"};

        /// Subbing/Release Groups in order of preference.
        /// If a file is not in the list, the compared files are considered comparatively equal.
        [JsonProperty("PreferredSubGroups")]
        public string[] _subgroups = { "fffpeeps", "doki", "commie", "horriblesubs" };

        /// Preferred Video Codecs, in order.
        /// Due to the complexity of Bit Depth, it has its own setting that is used when applicable.
        [JsonProperty("PreferredVideoCodecs")]
        public string[] _videocodecs =
        {
            "hevc", "h264", "mpeg4", "vc1", "flv", "mpeg2", "mpeg1", "vp6f"
        };

        /// Whether or not to prefer Bit Depth. If/when 12bit becomes more prominant, this can be changed to an integer.
        /// This will not apply to codecs that don't support.
        [JsonProperty]
        public bool Prefer8BitVideo { get; set; }

        /// <summary>
        /// Allow deletion of the file being imported.
        /// Main use case is if you set your client to dl everything and let Shoko pick the best
        /// If ServerSettings.FileQualityFilterEnabled is not true, this does nothing
        /// </summary>
        [JsonProperty]
        public bool AllowDeletionOfImportedFiles { get; set; }

        /// These are used to determine whether or not a file that isn't found to be the most preferred is kept.
        /// Each Setting must have an operation type, except for Types
        /// All checks must pass to keep a file.
        /// This is a list, in order, of the operations. Accepts no arguments.
        [JsonProperty("RequiredTypes")]
        public FileQualityFilterType[] _requiredtypes =
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.CHAPTER, FileQualityFilterType.VERSION
        };

        /// Required Audio Codec. Default must be FLAC, Dolby, or AAC.
        /// Accepts IN, NOTIN
        [JsonProperty("RequiredAudioCodecs")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredaudiocodecs =
            new Tuple<string[], FileQualityFilterOperationType>(
                new[] {"flac", "dca", "aac"},
                FileQualityFilterOperationType.IN);

        /// Required Audio Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        [JsonProperty("RequiredAudioStreamCount")]
        public Tuple<int, FileQualityFilterOperationType> _requiredaudiostreamcount =
            new Tuple<int, FileQualityFilterOperationType>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Resolution. Default must be 1080p or greater.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ, IN, NOTIN
        [JsonProperty("RequiredResolutions")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredresolutions =
            new Tuple<string[], FileQualityFilterOperationType>(new[] {"1080p"},
                FileQualityFilterOperationType.GREATER_EQ);

        /// Required Source. Default must be BD or DVD release.
        /// Accepts IN, NOTIN
        [JsonProperty("RequiredSources")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredsources =
            new Tuple<string[], FileQualityFilterOperationType>(new[] {"bd", "dvd"},
                FileQualityFilterOperationType.IN);

        /// The required Subbing/Release Groups and the operator. Defaulting to not HorribleSubs for example.
        /// Accepts IN, NOTIN
        [JsonProperty("RequiredSubGroups")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredsubgroups =
            new Tuple<string[], FileQualityFilterOperationType>(new[] { "horriblesubs" }, FileQualityFilterOperationType.NOTIN);

        /// Required Subtitle Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        [JsonProperty("RequiredSubStreamCount")]
        public Tuple<int, FileQualityFilterOperationType> _requiredsubstreamcount =
            new Tuple<int, FileQualityFilterOperationType>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Video Codec. Default must be H265/HEVC or H264/AVC.
        /// Accepts IN, NOTIN
        [JsonProperty("RequiredVideoCodecs")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredvideocodecs =
            new Tuple<string[], FileQualityFilterOperationType>(new[] {"hevc", "h264"},
                FileQualityFilterOperationType.IN);

        /// Require 10bit Video when applicable. This will not apply to codecs that don't support.
        [JsonProperty]
        public bool Require10BitVideo = true;

        /// The maximum number of files to keep per episode.
        /// I'll need to think of a way to handle episodes that are multipart, but listed as one episode.
        /// For now, just make sure to mark said files as variations, and it will not be deleted regardless.
        [JsonProperty]
        public int MaxNumberOfFilesToKeep = 1;

        #region public Getters and Setters
        /// This is a list, in order, of the operations to compare. Accepts no arguments.
        [JsonIgnore]
        public List<FileQualityFilterType> TypePreferences
        {
            get => _types.ToList();
            set => _types = value.ToArray();
        }

        /// Preferred Audio Codecs, in order.
        [JsonIgnore]
        public List<string> AudioCodecPreferences
        {
            get => _audiocodecs.ToList();
            set => _audiocodecs = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }

        /// Preferred Resolutions, in order.
        [JsonIgnore]
        public List<string> ResolutionPreferences
        {
            get => _resolutions.ToList();
            set => _resolutions = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }

        /// Preferred Sources, in order.
        [JsonIgnore]
        public List<string> SourcePreferences
        {
            get => _sources.ToList();
            set => _sources = value.Where(a => !string.IsNullOrEmpty(a)).Select(a =>
            {
                string source = a.ToLowerInvariant();
                if (SimplifiedSources.ContainsKey(source)) source = SimplifiedSources[source];
                return source;
            }).ToArray();
        }


        [JsonIgnore]
        public List<string> SubGroupPreferences
        {
            get => _subgroups.ToList();
            set => _subgroups = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }


        [JsonIgnore]
        public List<string> VideoCodecPreferences
        {
            get => _videocodecs.ToList();
            set => _videocodecs = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }


        [JsonIgnore]
        public List<FileQualityFilterType> RequiredTypes
        {
            get => _requiredtypes.ToList();
            set => _requiredtypes = value.ToArray();
        }


        [JsonIgnore]
        public List<string> RequiredAudioCodecs
        {
            get => _requiredaudiocodecs.Item1.ToList();
            set => _requiredaudiocodecs = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredaudiocodecs.Item2);
        }

        [JsonIgnore]
        public FileQualityFilterOperationType RequiredAudioCodecOperator
        {
            get => _requiredaudiocodecs.Item2;
            set => _requiredaudiocodecs =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredaudiocodecs.Item1, value);
        }

        [JsonIgnore]
        public int RequiredAudioStreamCount
        {
            get => _requiredaudiostreamcount.Item1;
            set => _requiredaudiostreamcount =
                new Tuple<int, FileQualityFilterOperationType>(value, _requiredaudiocodecs.Item2);
        }

        [JsonIgnore]
        public FileQualityFilterOperationType RequiredAudioStreamCountOperator
        {
            get => _requiredaudiostreamcount.Item2;
            set => _requiredaudiostreamcount =
                new Tuple<int, FileQualityFilterOperationType>(_requiredaudiostreamcount.Item1, value);
        }

        [JsonIgnore]
        public List<string> RequiredResolutions
        {
            get => _requiredresolutions.Item1.ToList();
            set => _requiredresolutions = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredresolutions.Item2);
        }


        [JsonIgnore]
        public FileQualityFilterOperationType RequiredResolutionOperator
        {
            get => _requiredresolutions.Item2;
            set => _requiredresolutions =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredresolutions.Item1, value);
        }


        [JsonIgnore]
        public List<string> RequiredSources
        {
            get => _requiredsources.Item1.ToList();
            set => _requiredsources = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredsources.Item2);
        }


        [JsonIgnore]
        public FileQualityFilterOperationType RequiredSourceOperator
        {
            get => _requiredsources.Item2;
            set => _requiredsources = new Tuple<string[], FileQualityFilterOperationType>(_requiredsources.Item1, value);
        }

        [JsonIgnore]
        public List<string> RequiredSubGroups
        {
            get => _requiredsubgroups.Item1.ToList();
            set => _requiredsubgroups = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredsubgroups.Item2);
        }

        [JsonIgnore]
        public FileQualityFilterOperationType RequiredSubGroupOperator
        {
            get => _requiredsubgroups.Item2;
            set => _requiredsubgroups =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredsubgroups.Item1, value);
        }

        [JsonIgnore]
        public int RequiredSubStreamCount
        {
            get => _requiredsubstreamcount.Item1;
            set => _requiredsubstreamcount =
                new Tuple<int, FileQualityFilterOperationType>(value, _requiredsubstreamcount.Item2);
        }

        [JsonIgnore]
        public FileQualityFilterOperationType RequiredSubStreamCountOperator
        {
            get => _requiredsubstreamcount.Item2;
            set => _requiredsubstreamcount =
                new Tuple<int, FileQualityFilterOperationType>(_requiredsubstreamcount.Item1, value);
        }

        [JsonIgnore]
        public List<string> RequiredVideoCodecs
        {
            get => _requiredvideocodecs.Item1.ToList();
            set => _requiredvideocodecs = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredvideocodecs.Item2);
        }

        [JsonIgnore]
        public FileQualityFilterOperationType RequiredVideoCodecOperator
        {
            get => _requiredvideocodecs.Item2;
            set => _requiredvideocodecs = new Tuple<string[], FileQualityFilterOperationType>(_requiredvideocodecs.Item1, value);
        }
        #endregion
    }
}
