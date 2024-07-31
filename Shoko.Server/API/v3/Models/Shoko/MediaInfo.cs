using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.MediaInfo;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

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

    public MediaInfo(SVR_VideoLocal file, MediaContainer mediaContainer)
    {
        var general = mediaContainer.GeneralStream;
        Title = general.Title;
        Duration = file.DurationTimeSpan;
        BitRate = general.OverallBitRate;
        FrameRate = general.FrameRate;
        Encoded = general.Encoded_Date?.ToUniversalTime();
        Audio = mediaContainer.AudioStreams
            .Select(audio => new AudioStreamInfo(audio))
            .ToList();
        Video = mediaContainer.VideoStreams
            .Select(video => new VideoStreamInfo(video))
            .ToList();
        Subtitles = mediaContainer.TextStreams
            .Select(text => new TextStreamInfo(text))
            .ToList();
        Chapters = [];
        FileExtension = general.FileExtension;
        MediaContainer = general.Format;
        MediaContainerVersion = general.Format_Version;
        var menu = mediaContainer.MenuStreams.FirstOrDefault();
        if (menu?.extra != null)
        {
            foreach (var (key, value) in menu.extra)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                var (hours, minutes, seconds, milliseconds) = key[1..].Split('_');
                if (!TimeSpan.TryParse($"{hours}:{minutes}:{seconds}.{milliseconds}", out var timestamp))
                    continue;
                var index = value.IndexOf(':');
                var title = string.IsNullOrEmpty(value) ? string.Empty : index != -1 ? value[(index + 1)..].Trim() : value.Trim();
                var language = index == -1 || index == 0 ? TitleLanguage.Unknown : value[..index].GetTitleLanguage();
                var chapterInfo = new ChapterInfo(title, language, timestamp);
                Chapters.Add(chapterInfo);
            }
        }
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

        public StreamInfo(Stream stream)
        {
            ID = stream.ID;
            UID = stream.UniqueID;
            Title = stream.Title;
            Order = stream.StreamOrder;
            IsDefault = stream.Default;
            IsForced = stream.Forced;
            Language = stream.Language?.GetTitleLanguage();
            LanguageCode = stream.LanguageCode;
            Codec = new StreamCodecInfo(stream);
            Format = new StreamFormatInfo(stream);
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

        public StreamFormatInfo(Stream stream)
        {
            Name = stream.Format?.ToLowerInvariant() ?? "unknown";
            Profile = stream.Format_Profile?.ToLowerInvariant();
            Level = stream.Format_Level?.ToLowerInvariant();
            Settings = stream.Format_Settings;
            AdditionalFeatures = stream.Format_AdditionalFeatures;
            Endianness = stream.Format_Settings_Endianness;
            Tier = stream.Format_Tier;
            Commercial = stream.Format_Commercial_IfAny;
            if (stream is VideoStream videoStream)
            {
                HDR = videoStream.HDR_Format;
                HDRCompatibility = videoStream.HDR_Format_Compatibility;
                CABAC = videoStream.Format_Settings_CABAC;
                BVOP = videoStream.Format_Settings_BVOP;
                QPel = videoStream.Format_Settings_QPel;
                GMC = videoStream.Format_Settings_GMC;
                ReferenceFrames = videoStream.Format_Settings_RefFrames;
            }
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

        public StreamCodecInfo(Stream stream)
        {
            Name = stream.Codec;
            Simplified = LegacyMediaUtils.TranslateCodec(stream)?.ToLowerInvariant() ?? "unknown";
            Raw = stream.CodecID?.ToLowerInvariant();
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
        /// Standarized resolution.
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

        public VideoStreamInfo(VideoStream stream) : base(stream)
        {
            Width = stream.Width;
            Height = stream.Height;
            Resolution = MediaInfoUtils.GetStandardResolution(new Tuple<int, int>(Width, Height));
            PixelAspectRatio = stream.PixelAspectRatio;
            FrameRate = stream.FrameRate;
            FrameRateMode = stream.FrameRate_Mode;
            FrameCount = stream.FrameCount;
            ScanType = stream.ScanType;
            ColorSpace = stream.ColorSpace;
            ChromaSubsampling = stream.ChromaSubsampling;
            MatrixCoefficients = stream.matrix_coefficients;
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

        public AudioStreamInfo(AudioStream stream) : base(stream)
        {
            Channels = stream.Channels;
            ChannelLayout = stream.ChannelLayout;
            SamplesPerFrame = stream.SamplesPerFrame;
            SamplingRate = stream.SamplingRate;
            CompressionMode = stream.Compression_Mode;
            BitRate = stream.BitRate;
            BitRateMode = stream.BitRate_Mode;
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

        public TextStreamInfo(TextStream stream) : base(stream)
        {
            SubTitle = stream.SubTitle;
            IsExternal = stream.External;
            if (stream.External && !string.IsNullOrEmpty(stream.Filename))
                ExternalFilename = stream.Filename;
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

        public ChapterInfo(string title, TitleLanguage language, TimeSpan timestamp)
        {
            Title = title;
            Language = language;
            Timestamp = timestamp;
        }
    }
}
