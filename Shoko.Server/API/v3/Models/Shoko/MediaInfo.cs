using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Parsed information from a <see cref="MediaContainer"/>.
/// </summary>
public class MediaInfo
{
    /// <summary>
    /// General title for the media.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Overall duration of the media.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Overall bit-rate across all streams in the media container.
    /// </summary>
    public int BitRate { get; }

    /// <summary>
    /// Average frame-rate across all the streams in the media container.
    /// </summary>
    public decimal FrameRate { get; }

    /// <summary>
    /// Date when encoding took place, if known.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Encoded { get; }

    /// <summary>
    /// True if the media is streaming-friendly.
    /// </summary>
    public bool IsStreamable { get; }

    /// <summary>
    /// Common file extension for the media container format.
    /// </summary>
    public string FileExtension { get; }

    /// <summary>
    /// The media container format.
    /// </summary>
    public string MediaContainer { get; }

    /// <summary>
    /// The media container format version.
    /// </summary>
    public int MediaContainerVersion { get; }

    /// <summary>
    /// Video streams in the media container.
    /// </summary>
    public List<VideoStreamInfo> Video { get; }

    /// <summary>
    /// Audio streams in the media container.
    /// </summary>
    public List<AudioStreamInfo> Audio { get; }

    /// <summary>
    /// Sub-title (text) streams in the media container.
    /// </summary>
    public List<TextStreamInfo> Subtitles { get; }

    /// <summary>
    /// Chapter information present in the media container.
    /// </summary>
    public List<ChapterInfo> Chapters { get; }

    public MediaInfo(VideoLocal file, IMediaInfo mediaInfo)
    {
        Title = mediaInfo.Title;
        Duration = file.DurationTimeSpan;
        BitRate = mediaInfo.BitRate;
        FrameRate = mediaInfo.FrameRate;
        Encoded = mediaInfo.Encoded?.ToUniversalTime();
        Audio = mediaInfo.AudioStreams
            .Select(audio => new AudioStreamInfo(audio))
            .ToList();
        Video = mediaInfo.VideoStreams
            .Select(video => new VideoStreamInfo(video))
            .ToList();
        Subtitles = mediaInfo.TextStreams
            .Select(text => new TextStreamInfo(text))
            .ToList();
        Chapters = mediaInfo.Chapters
            .Select(chapter => new ChapterInfo(chapter))
            .ToList();
        FileExtension = mediaInfo.FileExtension;
        MediaContainer = mediaInfo.ContainerName;
        MediaContainerVersion = mediaInfo.ContainerVersion;
    }

    public class StreamInfo
    {
        /// <summary>
        /// Local id for the stream.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Unique id for the stream.
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        /// Stream title, if available.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Stream order.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// True if this is the default stream of the given type.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// True if the stream is forced to be used.
        /// </summary>
        public bool IsForced { get; set; }

        /// <summary>
        /// <see cref="TitleLanguage"/> name of the language of the stream.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TitleLanguage? Language { get; }

        /// <summary>
        /// 3 character language code of the language of the stream.
        /// </summary>
        public string? LanguageCode { get; }

        /// <summary>
        /// Stream codec information.
        /// </summary>
        public StreamCodecInfo Codec { get; }

        /// <summary>
        /// Stream format information.
        /// </summary>
        public StreamFormatInfo Format { get; }

        public StreamInfo(IStream stream)
        {
            ID = stream.ID;
            UID = stream.UID;
            Title = stream.Title;
            Order = stream.Order;
            IsDefault = stream.IsDefault;
            IsForced = stream.IsForced;
            Language = stream.Language;
            LanguageCode = stream.LanguageCode;
            Codec = new StreamCodecInfo(stream.Codec);
            Format = new StreamFormatInfo(stream.Format);
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class StreamFormatInfo
    {
        /// <summary>
        /// Name of the format used.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Profile name of the format used, if available.
        /// </summary>
        public string? Profile { get; }

        /// <summary>
        /// Compression level of the format used, if available.
        /// </summary>
        public string? Level { get; }

        /// <summary>
        /// Format settings, if available.
        /// </summary>
        public string? Settings { get; }

        /// <summary>
        /// Known additional features enabled for the format, if available.
        /// </summary>
        public string? AdditionalFeatures { get; }

        /// <summary>
        /// Format endianness, if available.
        /// </summary>
        public string? Endianness { get; }

        /// <summary>
        /// Format tier, if available.
        /// </summary>
        public string? Tier { get; }

        /// <summary>
        /// Format commercial information, if available.
        /// </summary>
        public string? Commercial { get; }

        /// <summary>
        /// HDR format information, if available.
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public string? HDR { get; }

        /// <summary>
        /// HDR format compatibility information, if available.
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public string? HDRCompatibility { get; }

        /// <summary>
        /// Context-adaptive binary arithmetic coding (CABAC).
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public bool? CABAC { get; }

        /// <summary>
        /// Bi-directional video object planes (BVOP).
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public bool? BVOP { get; }

        /// <summary>
        /// Quarter-pixel motion (Qpel).
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public bool? QPel { get; }

        /// <summary>
        /// Global Motion Compensation (GMC) mode, if available.
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public string? GMC { get; }

        /// <summary>
        /// Reference frames count, if known.
        /// </summary>
        /// <remarks>
        /// Only available for <see cref="VideoStreamInfo"/>.
        /// </remarks>
        public int? ReferenceFrames { get; }

        public StreamFormatInfo(IStreamFormatInfo info)
        {
            Name = info.Name;
            Profile = info.Profile;
            Level = info.Level;
            Settings = info.Settings;
            AdditionalFeatures = info.AdditionalFeatures;
            Endianness = info.Endianness;
            Tier = info.Tier;
            Commercial = info.Commercial;
            HDR = info.HDR;
            HDRCompatibility = info.HDRCompatibility;
            CABAC = info.CABAC;
            BVOP = info.BVOP;
            QPel = info.QPel;
            GMC = info.GMC;
            ReferenceFrames = info.ReferenceFrames;
        }
    }

    public class StreamCodecInfo
    {
        /// <summary>
        /// Codec name, if available.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; }

        /// <summary>
        /// Simplified codec id.
        /// </summary>
        public string Simplified { get; }

        /// <summary>
        /// Raw codec id.
        /// </summary>
        public string? Raw { get; }

        public StreamCodecInfo(IStreamCodecInfo codec)
        {
            Name = codec.Name;
            Simplified = codec.Simplified;
            Raw = codec.Raw;
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class VideoStreamInfo : StreamInfo
    {
        /// <summary>
        /// Width of the video stream.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Height of the video stream.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Standardized resolution.
        /// </summary>
        public string Resolution { get; }

        /// <summary>
        /// Pixel aspect-ratio.
        /// </summary>
        public decimal PixelAspectRatio { get; }

        /// <summary>
        /// Frame-rate.
        /// </summary>
        public decimal FrameRate { get; }

        /// <summary>
        /// Frame-rate mode.
        /// </summary>
        public string FrameRateMode { get; }

        /// <summary>
        /// Total number of frames in the video stream.
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// Scan-type. Interlaced or progressive.
        /// </summary>
        public string ScanType { get; }

        /// <summary>
        /// Color-space.
        /// </summary>
        public string ColorSpace { get; }

        /// <summary>
        /// Chroma sub-sampling.
        /// </summary>
        public string ChromaSubsampling { get; }

        /// <summary>
        /// Matrix co-efficiencies.
        /// </summary>
        public string? MatrixCoefficients { get; }

        /// <summary>
        /// Bit-rate of the video stream.
        /// </summary>
        public int BitRate { get; }

        /// <summary>
        /// Bit-depth of the video stream.
        /// </summary>
        public int BitDepth { get; }

        public VideoStreamInfo(IVideoStream stream) : base(stream)
        {
            Width = stream.Width;
            Height = stream.Height;
            Resolution = stream.Resolution;
            PixelAspectRatio = stream.PixelAspectRatio;
            FrameRate = stream.FrameRate;
            FrameRateMode = stream.FrameRateMode;
            FrameCount = stream.FrameCount;
            ScanType = stream.ScanType;
            ColorSpace = stream.ColorSpace;
            ChromaSubsampling = stream.ChromaSubsampling;
            MatrixCoefficients = stream.MatrixCoefficients;
            BitRate = stream.BitRate;
            BitDepth = stream.BitDepth;
        }
    }

    public class AudioStreamInfo : StreamInfo
    {
        /// <summary>
        /// Number of total channels in the audio stream.
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// A text representation of the layout of the channels available in the
        /// audio stream.
        /// </summary>
        public string ChannelLayout { get; }

        /// <summary>
        /// Samples per frame.
        /// </summary>
        public int SamplesPerFrame { get; }

        /// <summary>
        /// Sampling rate of the audio.
        /// </summary>
        public int SamplingRate { get; }

        /// <summary>
        /// Compression mode used.
        /// </summary>
        public string CompressionMode { get; }

        /// <summary>
        /// Bit-rate of the audio-stream.
        /// </summary>
        public int BitRate { get; }

        /// <summary>
        /// Bit-rate mode of the audio stream.
        /// </summary>
        public string BitRateMode { get; }

        /// <summary>
        /// Bit-depth of the audio stream.
        /// </summary>
        public int BitDepth { get; }

        public AudioStreamInfo(IAudioStream stream) : base(stream)
        {
            Channels = stream.Channels;
            ChannelLayout = stream.ChannelLayout;
            SamplesPerFrame = stream.SamplesPerFrame;
            SamplingRate = stream.SamplingRate;
            CompressionMode = stream.CompressionMode;
            BitRate = stream.BitRate;
            BitRateMode = stream.BitRateMode;
            BitDepth = stream.BitDepth;
        }
    }

    public class TextStreamInfo : StreamInfo
    {
        /// <summary>
        /// Sub-title of the text stream.
        /// </summary>
        /// <value></value>
        public string? SubTitle { get; }

        /// <summary>
        /// Not From MediaInfo. Is this an external sub file
        /// </summary>
        public bool IsExternal { get; }

        /// <summary>
        /// The name of the external subtitle file if this is stream is from an
        /// external source. This field is only sent if <see cref="IsExternal"/>
        /// is set to <code>true</code>.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ExternalFilename { get; }

        public TextStreamInfo(ITextStream stream) : base(stream)
        {
            SubTitle = stream.SubTitle;
            IsExternal = stream.IsExternal;
            if (stream.IsExternal && !string.IsNullOrEmpty(stream.ExternalFilename))
                ExternalFilename = stream.ExternalFilename;
        }
    }

    public class ChapterInfo
    {
        /// <summary>
        /// Chapter title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Chapter title language, if specified.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TitleLanguage Language { get; }

        /// <summary>
        /// Chapter timestamp.
        /// </summary>
        public TimeSpan Timestamp { get; }

        public ChapterInfo(IChapterInfo info)
        {
            Title = info.Title;
            Language = info.Language;
            Timestamp = info.Timestamp;
        }
    }
}
