using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Shoko.Models.Enums;

namespace Shoko.Models
{
    [DataContract]
    public class FileQualityPreferences
    {
        public FileQualityPreferences()
        {

        }

        /// This is a list, in order, of the operations to compare. Accepts no arguments.
        [DataMember(Name = "Types")]
        public FileQualityFilterType[] _types =
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.RESOLUTION, FileQualityFilterType.AUDIOSTREAMCOUNT,
            FileQualityFilterType.SUBSTREAMCOUNT, FileQualityFilterType.SUBGROUP, FileQualityFilterType.CHAPTER,
            FileQualityFilterType.VIDEOCODEC, FileQualityFilterType.AUDIOCODEC, FileQualityFilterType.VERSION
        };

        /// Preferred Audio Codecs, in order.
        [DataMember(Name = "PreferredAudioCodecs")]
        public string[] _audiocodecs =
        {
            "flac", "dolby digital plus", "dolby truehd", "dts", "he-aac", "aac", "ac3", "mp3 cbr", "mp3 vbr",
            "vorbis (ogg vorbis)", "wma (also divx audio)", "realaudio g2/8 (cook)", "alac", "msaudio", "opus", "mp2",
            "pcm", "unknown", "none"
        };

        /// Preferred Resolutions, in order.
        [DataMember(Name = "PreferredResolutions")]
        public string[] _resolutions = {"2160p", "1440p", "1080p", "720p", "480p"};

        /// Preferred Sources, in order.
        [DataMember(Name = "PreferredSources")]
        public string[] _sources = {"bd", "dvd", "hdtv", "tv", "www", "unknown"};

        /// Subbing/Release Groups in order of preference.
        /// If a file is not in the list, the compared files are considered comparatively equal.
        [DataMember(Name = "PreferredSubGroups")]
        public string[] _subgroups = { "fffpeeps", "doki", "commie", "horriblesubs" };

        /// Preferred Video Codecs, in order.
        /// Due to the complexity of Bit Depth, it has its own setting that is used when applicable.
        [DataMember(Name = "PreferredVideoCodecs")]
        public string[] _videocodecs =
        {
            "hevc", "h264/avc", "divx5/6", "mpeg-4 asp", "mpeg-2", "mpeg-1", "ms mp4x", "divx4", "divx3", "vc-1",
            "xvid", "realvideo 9/10", "other (divx)", "other (mpeg-4 sp)", "other (realvideo)", "vp8", "vp6",
            "other (vpx)", "other (wmv3/wmv/9)", "unknown", "none"
        };

        /// Whether or not to prefer Bit Depth. If/when 12bit becomes more prominant, this can be changed to an integer.
        /// This will not apply to codecs that don't support.
        [DataMember]
        public bool Prefer8BitVideo = false;

        /// <summary>
        /// Allow deletion of the file being imported.
        /// Main use case is if you set your client to dl everything and let Shoko pick the best
        /// If ServerSettings.FileQualityFilterEnabled is not true, this does nothing
        /// </summary>
        [DataMember]
        public bool AllowDeletionOfImportedFiles = false;

        /// These are used to determine whether or not a file that isn't found to be the most preferred is kept.
        /// Each Setting must have an operation type, except for Types
        /// All checks must pass to keep a file.
        /// This is a list, in order, of the operations. Accepts no arguments.
        [DataMember(Name = "RequiredTypes")]
        public FileQualityFilterType[] _requiredtypes =
        {
            FileQualityFilterType.SOURCE, FileQualityFilterType.CHAPTER, FileQualityFilterType.VERSION
        };

        /// Required Audio Codec. Default must be FLAC, Dolby, or AAC.
        /// Accepts IN, NOTIN
        [DataMember(Name = "RequiredAudioCodecs")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredaudiocodecs =
            new Tuple<string[], FileQualityFilterOperationType>(
                new[] {"flac", "dolby digital plus", "dolby truehd", "dts", "aac"},
                FileQualityFilterOperationType.IN);

        /// Required Audio Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        [DataMember(Name = "RequiredAudioStreamCount")]
        public Tuple<int, FileQualityFilterOperationType> _requiredaudiostreamcount =
            new Tuple<int, FileQualityFilterOperationType>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Resolution. Default must be 1080p or greater.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ, IN, NOTIN
        [DataMember(Name = "RequiredResolutions")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredresolutions =
            new Tuple<string[], FileQualityFilterOperationType>(new string[] {"1080p"},
                FileQualityFilterOperationType.GREATER_EQ);

        /// Required Source. Default must be BD or DVD release.
        /// Accepts IN, NOTIN
        [DataMember(Name = "RequiredSources")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredsources =
            new Tuple<string[], FileQualityFilterOperationType>(new string[] {"bd", "dvd"},
                FileQualityFilterOperationType.IN);

        /// The required Subbing/Release Groups and the operator. Defaulting to not HorribleSubs for example.
        /// Accepts IN, NOTIN
        [DataMember(Name = "RequiredSubGroups")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredsubgroups =
            new Tuple<string[], FileQualityFilterOperationType>(new string[] { "horriblesubs" }, FileQualityFilterOperationType.NOTIN);

        /// Required Subtitle Stream Count. Default is >= 1.
        /// Accepts EQUAL, GREATER_EQ, LESS_EQ
        [DataMember(Name = "RequiredSubStreamCount")]
        public Tuple<int, FileQualityFilterOperationType> _requiredsubstreamcount =
            new Tuple<int, FileQualityFilterOperationType>(1, FileQualityFilterOperationType.GREATER_EQ);

        /// Required Video Codec. Default must be H265/HEVC or H264/AVC.
        /// Accepts IN, NOTIN
        [DataMember(Name = "RequiredVideoCodecs")]
        public Tuple<string[], FileQualityFilterOperationType> _requiredvideocodecs =
            new Tuple<string[], FileQualityFilterOperationType>(new string[] {"hevc", "h264/avc"},
                FileQualityFilterOperationType.IN);

        /// Require 10bit Video when applicable. This will not apply to codecs that don't support.
        [DataMember]
        public bool Require10BitVideo = true;

        /// The maximum number of files to keep per episode.
        /// I'll need to think of a way to handle episodes that are multipart, but listed as one episode.
        /// For now, just make sure to mark said files as variations, and it will not be deleted regardless.
        [DataMember]
        public int MaxNumberOfFilesToKeep = 1;

        #region public Getters and Setters
        /// This is a list, in order, of the operations to compare. Accepts no arguments.
        [IgnoreDataMember]
        public List<FileQualityFilterType> TypePreferences
        {
            get => _types.ToList();
            set => _types = value.ToArray();
        }

        /// Preferred Audio Codecs, in order.
        [IgnoreDataMember]
        public List<string> AudioCodecPreferences
        {
            get => _audiocodecs.ToList();
            set => _audiocodecs = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }

        /// Preferred Resolutions, in order.
        [IgnoreDataMember]
        public List<string> ResolutionPreferences
        {
            get => _resolutions.ToList();
            set => _resolutions = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }

        /// Preferred Sources, in order.
        [IgnoreDataMember]
        public List<string> SourcePreferences
        {
            get => _sources.ToList();
            set => _sources = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }


        [IgnoreDataMember]
        public List<string> SubGroupPreferences
        {
            get => _subgroups.ToList();
            set => _subgroups = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }


        [IgnoreDataMember]
        public List<string> VideoCodecPreferences
        {
            get => _videocodecs.ToList();
            set => _videocodecs = value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray();
        }


        [IgnoreDataMember]
        public List<FileQualityFilterType> RequiredTypes
        {
            get => _requiredtypes.ToList();
            set => _requiredtypes = value.ToArray();
        }


        [IgnoreDataMember]
        public List<string> RequiredAudioCodecs
        {
            get => _requiredaudiocodecs.Item1.ToList();
            set => _requiredaudiocodecs = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredaudiocodecs.Item2);
        }

        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredAudioCodecOperator
        {
            get => _requiredaudiocodecs.Item2;
            set => _requiredaudiocodecs =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredaudiocodecs.Item1, value);
        }

        [IgnoreDataMember]
        public int RequiredAudioStreamCount
        {
            get => _requiredaudiostreamcount.Item1;
            set => _requiredaudiostreamcount =
                new Tuple<int, FileQualityFilterOperationType>(value, _requiredaudiocodecs.Item2);
        }

        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredAudioStreamCountOperator
        {
            get => _requiredaudiostreamcount.Item2;
            set => _requiredaudiostreamcount =
                new Tuple<int, FileQualityFilterOperationType>(_requiredaudiostreamcount.Item1, value);
        }

        [IgnoreDataMember]
        public List<string> RequiredResolutions
        {
            get => _requiredresolutions.Item1.ToList();
            set => _requiredresolutions = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredresolutions.Item2);
        }


        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredResolutionOperator
        {
            get => _requiredresolutions.Item2;
            set => _requiredresolutions =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredresolutions.Item1, value);
        }


        [IgnoreDataMember]
        public List<string> RequiredSources
        {
            get => _requiredsources.Item1.ToList();
            set => _requiredsources = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredsources.Item2);
        }


        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredSourceOperator
        {
            get => _requiredsources.Item2;
            set => _requiredsources = new Tuple<string[], FileQualityFilterOperationType>(_requiredsources.Item1, value);
        }

        [IgnoreDataMember]
        public List<string> RequiredSubGroups
        {
            get => _requiredsubgroups.Item1.ToList();
            set => _requiredsubgroups = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredsubgroups.Item2);
        }

        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredSubGroupOperator
        {
            get => _requiredsubgroups.Item2;
            set => _requiredsubgroups =
                new Tuple<string[], FileQualityFilterOperationType>(_requiredsubgroups.Item1, value);
        }

        [IgnoreDataMember]
        public int RequiredSubStreamCount
        {
            get => _requiredsubstreamcount.Item1;
            set => _requiredsubstreamcount =
                new Tuple<int, FileQualityFilterOperationType>(value, _requiredsubstreamcount.Item2);
        }

        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredSubStreamCountOperator
        {
            get => _requiredsubstreamcount.Item2;
            set => _requiredsubstreamcount =
                new Tuple<int, FileQualityFilterOperationType>(_requiredsubstreamcount.Item1, value);
        }

        [IgnoreDataMember]
        public List<string> RequiredVideoCodecs
        {
            get => _requiredvideocodecs.Item1.ToList();
            set => _requiredvideocodecs = new Tuple<string[], FileQualityFilterOperationType>(
                value.Where(a => !string.IsNullOrEmpty(a)).Select(a => a.ToLowerInvariant()).ToArray(),
                _requiredvideocodecs.Item2);
        }

        [IgnoreDataMember]
        public FileQualityFilterOperationType RequiredVideoCodecOperator
        {
            get => _requiredvideocodecs.Item2;
            set => _requiredvideocodecs = new Tuple<string[], FileQualityFilterOperationType>(_requiredvideocodecs.Item1, value);
        }
        #endregion
    }
}