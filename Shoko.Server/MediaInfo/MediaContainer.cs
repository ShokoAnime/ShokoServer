using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Media;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
#pragma warning disable IDE0019
#pragma warning disable IDE1006
#pragma warning disable MsgPack015
#nullable enable
namespace Shoko.Server.MediaInfo;

/// <summary>
/// This is a properly named Media container class. Names here respect MediaInfo's naming.
/// Because of this, they can be looked up on https://mediaarea.net/en/MediaInfo/Support/Tags, then converted as needed by specific clients.
/// If the client is open source, then usually, they already have mappings, as most clients use MediaInfo, anyway.
/// </summary>
[MessagePackObject]
public class MediaContainer : IMediaInfo
{
    [Key(0)]
    public Media? media { get; set; }

    // Cache to prevent excessive enumeration on things that will be called A LOT
    [IgnoreMember]
    private GeneralStream? _general = null;

    [IgnoreMember]
    private VideoStream? _video = null;

    [IgnoreMember]
    private List<VideoStream>? _videos = null;

    [IgnoreMember]
    private List<AudioStream>? _audios = null;

    [IgnoreMember]
    private List<TextStream>? _texts = null;

    [IgnoreMember]
    private List<MenuStream>? _menus = null;

    [IgnoreMember]
    private List<ChapterInfo>? _chapters = null;

    [JsonIgnore]
    [IgnoreMember]
    [MemberNotNullWhen(true, nameof(GeneralStream))]
    [MemberNotNullWhen(true, nameof(media))]
    public bool IsUsable => GeneralStream is { Duration: > 0 };

    [JsonIgnore]
    [IgnoreMember]
    public GeneralStream GeneralStream
    {
        get
        {
            if (_general != null)
                return _general;
            var generalStream = media?.track?.FirstOrDefault(a => a?.type == StreamType.General) as GeneralStream ??
                throw new Exception("Unable to read general stream from media container.");
            return _general = generalStream;
        }
    }

    [JsonIgnore]
    [IgnoreMember]
    public VideoStream? VideoStream =>
        _video ??= media?.track?.FirstOrDefault(a => a?.type == StreamType.Video) as VideoStream;

    [JsonIgnore]
    [IgnoreMember]
    public List<VideoStream> VideoStreams =>
        _videos ??= media?.track?.Where(a => a?.type == StreamType.Video).Cast<VideoStream>().ToList() ?? [];

    [JsonIgnore]
    [IgnoreMember]
    public List<AudioStream> AudioStreams =>
        _audios ??= media?.track?.Where(a => a?.type == StreamType.Audio).Cast<AudioStream>().ToList() ?? [];

    [JsonIgnore]
    [IgnoreMember]
    public List<TextStream> TextStreams =>
        _texts ??= media?.track?.Where(a => a?.type == StreamType.Text).Cast<TextStream>().ToList() ?? [];

    [JsonIgnore]
    [IgnoreMember]
    public List<MenuStream> MenuStreams =>
        _menus ??= media?.track?.Where(a => a?.type == StreamType.Menu).Cast<MenuStream>().ToList() ?? [];

    [IgnoreMember]
    string? IMediaInfo.Title => GeneralStream.Title;

    [IgnoreMember]
    TimeSpan IMediaInfo.Duration
    {
        get
        {
            var duration = GeneralStream?.Duration ?? 0;
            var seconds = Math.Truncate(duration);
            var milliseconds = (duration - seconds) * 1000;
            return new TimeSpan(0, 0, 0, (int)seconds, (int)milliseconds);
        }
    }

    [IgnoreMember]
    int IMediaInfo.BitRate => GeneralStream.OverallBitRate;

    [IgnoreMember]
    decimal IMediaInfo.FrameRate => GeneralStream.FrameRate;

    [IgnoreMember]
    DateTime? IMediaInfo.Encoded => GeneralStream.Encoded_Date;

    [IgnoreMember]
    bool IMediaInfo.IsStreamable => GeneralStream.IsStreamable;

    [IgnoreMember]
    string IMediaInfo.FileExtension => GeneralStream.FileExtension;

    [IgnoreMember]
    string IMediaInfo.ContainerName => GeneralStream.Format;

    [IgnoreMember]
    int IMediaInfo.ContainerVersion =>
        GeneralStream.Format_Version;

    [IgnoreMember]
    IVideoStream? IMediaInfo.VideoStream => VideoStream;

    [IgnoreMember]
    IReadOnlyList<IVideoStream> IMediaInfo.VideoStreams =>
        VideoStreams;

    [IgnoreMember]
    IReadOnlyList<IAudioStream> IMediaInfo.AudioStreams =>
        AudioStreams;

    [IgnoreMember]
    IReadOnlyList<ITextStream> IMediaInfo.TextStreams =>
        TextStreams;

    [IgnoreMember]
    IReadOnlyList<string> IMediaInfo.Attachments =>
        GeneralStream.extra?.Attachments?.Split("/", StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray() ?? [];

    [IgnoreMember]
    IReadOnlyList<IChapterInfo> IMediaInfo.Chapters
    {
        get
        {
            if (_chapters != null)
                return _chapters;
            var chapters = new List<ChapterInfo>();
            var menu = MenuStreams.FirstOrDefault();
            if (menu?.extra != null)
            {
                foreach (var (key, value) in menu.extra)
                {
                    if (string.IsNullOrEmpty(key))
                        continue;
                    var list = key[1..].Split('_');
                    if (list.Length < 4 || !TimeSpan.TryParse($"{list[0]}:{list[1]}:{list[2]}.{list[3]}", out var timestamp))
                        continue;
                    var index = value.IndexOf(':');
                    var title = string.IsNullOrEmpty(value) ? string.Empty : index != -1 ? value[(index + 1)..].Trim() : value.Trim();
                    var language = index is -1 or 0 ? null : value[..index];
                    var chapterInfo = new ChapterInfo(title, language, timestamp);
                    chapters.Add(chapterInfo);
                }
            }
            return _chapters = chapters;
        }
    }

    protected bool Equals(MediaContainer other) => Equals(media, other.media);

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((MediaContainer)obj);
    }

    public override int GetHashCode() => (media != null ? media.GetHashCode() : 0);
}
#nullable disable

[MessagePackObject]
public class Media
{
    [Key(0)]
    public List<Stream> track { get; set; }

    protected bool Equals(Media other) => track.SequenceEqual(other.track);

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Media)obj);
    }

    public override int GetHashCode() => (track != null ? track.GetHashCode() : 0);
}

public enum StreamType
{
    General,
    Video,
    Audio,
    Text,
    Menu
}

[MessagePackObject(true)]
[Union(0, typeof(GeneralStream))]
[Union(1, typeof(VideoStream))]
[Union(2, typeof(AudioStream))]
[Union(3, typeof(TextStream))]
[Union(4, typeof(MenuStream))]
public abstract class Stream : IStream
{
    /// <summary>
    /// I would make this an int, as most things give this a value of like 1 or 5, but there exist situations such as
    /// 235274903222604292645173588053004470927, and I don't have a 128-bit CPU...
    /// This isn't generally needed, anyway
    /// </summary>
    public string UniqueID { get; set; }

    public int ID { get; set; }

    public abstract StreamType type { get; }

    public string Title { get; set; }

    public int StreamOrder { get; set; }

    public string Format { get; set; }

    public string Format_Profile { get; set; }

    public string Format_Settings { get; set; }

    public string Format_Level { get; set; }

    public string Format_Commercial_IfAny { get; set; }

    public string Format_Tier { get; set; }

    public string Format_AdditionalFeatures { get; set; }

    public string Format_Settings_Endianness { get; set; }

    public string Codec { get; set; }

    public string CodecID { get; set; }

    /// <summary>
    /// The Language code (ISO 639-1 in everything I've seen) from MediaInfo
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// This is the 3 character language code
    /// This is mapped from the Language, it is not MediaInfo data
    /// </summary>
    public string LanguageCode { get; set; }

    /// <summary>
    /// This is the Language Name, "English"
    /// This is mapped from the Language, it is not MediaInfo data
    /// </summary>
    public string LanguageName { get; set; }

    public bool Default { get; set; }

    public bool Forced { get; set; }

    string IStream.UID => UniqueID;
    int IStream.ID => ID;
    int IStream.Order => StreamOrder;
    bool IStream.IsDefault => Default;
    bool IStream.IsForced => Forced;
    TitleLanguage IStream.Language => Language?.GetTitleLanguage() ?? TitleLanguage.None;
    IStreamCodecInfo IStream.Codec => new StreamCodecInfoImpl(this);
    IStreamFormatInfo IStream.Format => new StreamFormatInfoImpl(this);

    protected bool Equals(Stream other) =>
        UniqueID == other.UniqueID && ID == other.ID && type == other.type && Title == other.Title && StreamOrder == other.StreamOrder && Format == other.Format && Format_Profile == other.Format_Profile
        && Format_Settings == other.Format_Settings && Format_Level == other.Format_Level && Format_Commercial_IfAny == other.Format_Commercial_IfAny && Format_Tier == other.Format_Tier
        && Format_AdditionalFeatures == other.Format_AdditionalFeatures && Format_Settings_Endianness == other.Format_Settings_Endianness && Codec == other.Codec && CodecID == other.CodecID
        && Language == other.Language && LanguageCode == other.LanguageCode && LanguageName == other.LanguageName && Default == other.Default && Forced == other.Forced;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Stream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (UniqueID != null ? UniqueID.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ ID;
            hashCode = (hashCode * 397) ^ (int)type;
            hashCode = (hashCode * 397) ^ (Title != null ? Title.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ StreamOrder;
            hashCode = (hashCode * 397) ^ (Format != null ? Format.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Profile != null ? Format_Profile.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Settings != null ? Format_Settings.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Level != null ? Format_Level.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Commercial_IfAny != null ? Format_Commercial_IfAny.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Tier != null ? Format_Tier.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_AdditionalFeatures != null ? Format_AdditionalFeatures.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Format_Settings_Endianness != null ? Format_Settings_Endianness.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Codec != null ? Codec.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (CodecID != null ? CodecID.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Language != null ? Language.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (LanguageCode != null ? LanguageCode.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (LanguageName != null ? LanguageName.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Default.GetHashCode();
            hashCode = (hashCode * 397) ^ Forced.GetHashCode();
            return hashCode;
        }
    }
}

[MessagePackObject(true)]
public class GeneralStream : Stream
{
    public override StreamType type => StreamType.General;

    public double Duration { get; set; }

    public int OverallBitRate { get; set; }

    public string FileExtension { get; set; }

    public int Format_Version { get; set; }

    public decimal FrameRate { get; set; }

    public bool IsStreamable { get; set; }

    public DateTime? Encoded_Date { get; set; }

    public GeneralExtra extra { get; }

    protected bool Equals(GeneralStream other) =>
        base.Equals(other) && Duration.Equals(other.Duration) && OverallBitRate == other.OverallBitRate && FileExtension == other.FileExtension && Format_Version == other.Format_Version
        && FrameRate == other.FrameRate && IsStreamable == other.IsStreamable && Nullable.Equals(Encoded_Date, other.Encoded_Date);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((GeneralStream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ Duration.GetHashCode();
            hashCode = (hashCode * 397) ^ OverallBitRate;
            hashCode = (hashCode * 397) ^ (FileExtension != null ? FileExtension.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Format_Version;
            hashCode = (hashCode * 397) ^ FrameRate.GetHashCode();
            hashCode = (hashCode * 397) ^ IsStreamable.GetHashCode();
            hashCode = (hashCode * 397) ^ Encoded_Date.GetHashCode();
            return hashCode;
        }
    }
}


[MessagePackObject(true)]
public class GeneralExtra
{
    public string Attachments { get; set; }
}

[MessagePackObject(true)]
public class VideoStream : Stream, IVideoStream
{
    public override StreamType type => StreamType.Video;

    public bool Format_Settings_CABAC { get; set; }

    public bool Format_Settings_BVOP { get; set; }

    public bool Format_Settings_QPel { get; set; }

    public string Format_Settings_GMC { get; set; }

    public int Format_Settings_RefFrames { get; set; }

    public int BitRate { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public decimal PixelAspectRatio { get; set; }

    public decimal FrameRate { get; set; }

    public string FrameRate_Mode { get; set; }

    public int FrameCount { get; set; }

    public string ColorSpace { get; set; }

    public string ChromaSubsampling { get; set; }

    public int BitDepth { get; set; }

    public string ScanType { get; set; }

    public string Encoded_Library_Name { get; set; }

    public string MuxingMode { get; set; }

    // HDR stuff. Can be on SD, but not as common, and not very useful

    public string HDR_Format { get; set; }

    public string HDR_Format_Compatibility { get; set; }

    public string colour_range { get; set; }

    public string colour_primaries { get; set; }

    public string transfer_characteristics { get; set; }

    public string matrix_coefficients { get; set; }

    public string MasteringDisplay_ColorPrimaries { get; set; }

    public string MasteringDisplay_Luminance { get; set; }

    public string MaxCLL { get; set; }

    public string MaxFALL { get; set; }

    string IVideoStream.MatrixCoefficients => matrix_coefficients;
    string IVideoStream.FrameRateMode => FrameRate_Mode;
    string IVideoStream.Resolution => MediaInfoUtility.GetStandardResolution(Tuple.Create(Width, Height));
    IStreamMuxingInfo IVideoStream.Muxing => new StreamMuxingInfoImpl(this);

    protected bool Equals(VideoStream other) =>
        base.Equals(other) && Format_Settings_CABAC == other.Format_Settings_CABAC && Format_Settings_BVOP == other.Format_Settings_BVOP && Format_Settings_QPel == other.Format_Settings_QPel
        && Format_Settings_GMC == other.Format_Settings_GMC && Format_Settings_RefFrames == other.Format_Settings_RefFrames && BitRate == other.BitRate && Width == other.Width && Height == other.Height
        && PixelAspectRatio == other.PixelAspectRatio && FrameRate == other.FrameRate && FrameRate_Mode == other.FrameRate_Mode && FrameCount == other.FrameCount && ColorSpace == other.ColorSpace
        && ChromaSubsampling == other.ChromaSubsampling && BitDepth == other.BitDepth && ScanType == other.ScanType && Encoded_Library_Name == other.Encoded_Library_Name && MuxingMode == other.MuxingMode
        && HDR_Format == other.HDR_Format && HDR_Format_Compatibility == other.HDR_Format_Compatibility && colour_range == other.colour_range && colour_primaries == other.colour_primaries
        && transfer_characteristics == other.transfer_characteristics && matrix_coefficients == other.matrix_coefficients && MasteringDisplay_ColorPrimaries == other.MasteringDisplay_ColorPrimaries
        && MasteringDisplay_Luminance == other.MasteringDisplay_Luminance && MaxCLL == other.MaxCLL && MaxFALL == other.MaxFALL;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((VideoStream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ Format_Settings_CABAC.GetHashCode();
            hashCode = (hashCode * 397) ^ Format_Settings_BVOP.GetHashCode();
            hashCode = (hashCode * 397) ^ Format_Settings_QPel.GetHashCode();
            hashCode = (hashCode * 397) ^ (Format_Settings_GMC != null ? Format_Settings_GMC.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Format_Settings_RefFrames;
            hashCode = (hashCode * 397) ^ BitRate;
            hashCode = (hashCode * 397) ^ Width;
            hashCode = (hashCode * 397) ^ Height;
            hashCode = (hashCode * 397) ^ PixelAspectRatio.GetHashCode();
            hashCode = (hashCode * 397) ^ FrameRate.GetHashCode();
            hashCode = (hashCode * 397) ^ (FrameRate_Mode != null ? FrameRate_Mode.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ FrameCount;
            hashCode = (hashCode * 397) ^ (ColorSpace != null ? ColorSpace.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (ChromaSubsampling != null ? ChromaSubsampling.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ BitDepth;
            hashCode = (hashCode * 397) ^ (ScanType != null ? ScanType.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Encoded_Library_Name != null ? Encoded_Library_Name.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (MuxingMode != null ? MuxingMode.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (HDR_Format != null ? HDR_Format.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (HDR_Format_Compatibility != null ? HDR_Format_Compatibility.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (colour_range != null ? colour_range.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (colour_primaries != null ? colour_primaries.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (transfer_characteristics != null ? transfer_characteristics.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (matrix_coefficients != null ? matrix_coefficients.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (MasteringDisplay_ColorPrimaries != null ? MasteringDisplay_ColorPrimaries.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (MasteringDisplay_Luminance != null ? MasteringDisplay_Luminance.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (MaxCLL != null ? MaxCLL.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (MaxFALL != null ? MaxFALL.GetHashCode() : 0);
            return hashCode;
        }
    }
}

[MessagePackObject(true)]
public class AudioStream : Stream, IAudioStream
{
    public override StreamType type => StreamType.Audio;

    public int Channels { get; set; }

    public string ChannelLayout { get; set; }

    public int SamplesPerFrame { get; set; }

    public int SamplingRate { get; set; }

    public string Compression_Mode { get; set; }

    public int BitRate { get; set; }

    public string BitRate_Mode { get; set; }

    public int BitDepth { get; set; }

    public AudioExtra extra { get; set; }

    string IAudioStream.CompressionMode => Compression_Mode;
    string IAudioStream.BitRateMode => BitRate_Mode;
    double? IAudioStream.DialogNorm => extra?.dialnorm;

    protected bool Equals(AudioStream other) =>
        base.Equals(other) && Channels == other.Channels && ChannelLayout == other.ChannelLayout && SamplesPerFrame == other.SamplesPerFrame && SamplingRate == other.SamplingRate
        && Compression_Mode == other.Compression_Mode && BitRate == other.BitRate && BitRate_Mode == other.BitRate_Mode && BitDepth == other.BitDepth && Equals(extra, other.extra);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((AudioStream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ Channels;
            hashCode = (hashCode * 397) ^ (ChannelLayout != null ? ChannelLayout.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ SamplesPerFrame;
            hashCode = (hashCode * 397) ^ SamplingRate;
            hashCode = (hashCode * 397) ^ (Compression_Mode != null ? Compression_Mode.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ BitRate;
            hashCode = (hashCode * 397) ^ (BitRate_Mode != null ? BitRate_Mode.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ BitDepth;
            hashCode = (hashCode * 397) ^ (extra != null ? extra.GetHashCode() : 0);
            return hashCode;
        }
    }
}

[MessagePackObject(true)]
public class AudioExtra
{
    // Audio
    /// <summary>
    /// Atmos or other 3D audio
    /// </summary>
    public string NumberOfDynamicObjects { get; set; }

    public int bsid { get; set; }

    // Audio Compression and Normalization

    public double dialnorm { get; set; }

    public double compr { get; set; }

    public double dynrng { get; set; }

    public double acmod { get; set; }

    public double lfeon { get; set; }

    public double dialnorm_Average { get; set; }

    public double dialnorm_Minimum { get; set; }

    public double compr_Average { get; set; }

    public double compr_Minimum { get; set; }

    public double compr_Maximum { get; set; }

    public int compr_Count { get; set; }

    public double dynrng_Average { get; set; }

    public double dynrng_Minimum { get; set; }

    public double dynrng_Maximum { get; set; }

    public int dynrng_Count { get; set; }

    protected bool Equals(AudioExtra other) =>
        NumberOfDynamicObjects == other.NumberOfDynamicObjects && bsid == other.bsid && dialnorm.Equals(other.dialnorm) && compr.Equals(other.compr) && dynrng.Equals(other.dynrng) && acmod.Equals(other.acmod)
        && lfeon.Equals(other.lfeon) && dialnorm_Average.Equals(other.dialnorm_Average) && dialnorm_Minimum.Equals(other.dialnorm_Minimum) && compr_Average.Equals(other.compr_Average)
        && compr_Minimum.Equals(other.compr_Minimum) && compr_Maximum.Equals(other.compr_Maximum) && compr_Count == other.compr_Count && dynrng_Average.Equals(other.dynrng_Average)
        && dynrng_Minimum.Equals(other.dynrng_Minimum) && dynrng_Maximum.Equals(other.dynrng_Maximum) && dynrng_Count == other.dynrng_Count;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((AudioExtra)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (NumberOfDynamicObjects != null ? NumberOfDynamicObjects.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ bsid;
            hashCode = (hashCode * 397) ^ dialnorm.GetHashCode();
            hashCode = (hashCode * 397) ^ compr.GetHashCode();
            hashCode = (hashCode * 397) ^ dynrng.GetHashCode();
            hashCode = (hashCode * 397) ^ acmod.GetHashCode();
            hashCode = (hashCode * 397) ^ lfeon.GetHashCode();
            hashCode = (hashCode * 397) ^ dialnorm_Average.GetHashCode();
            hashCode = (hashCode * 397) ^ dialnorm_Minimum.GetHashCode();
            hashCode = (hashCode * 397) ^ compr_Average.GetHashCode();
            hashCode = (hashCode * 397) ^ compr_Minimum.GetHashCode();
            hashCode = (hashCode * 397) ^ compr_Maximum.GetHashCode();
            hashCode = (hashCode * 397) ^ compr_Count;
            hashCode = (hashCode * 397) ^ dynrng_Average.GetHashCode();
            hashCode = (hashCode * 397) ^ dynrng_Minimum.GetHashCode();
            hashCode = (hashCode * 397) ^ dynrng_Maximum.GetHashCode();
            hashCode = (hashCode * 397) ^ dynrng_Count;
            return hashCode;
        }
    }
}

[MessagePackObject(true)]
public class TextStream : Stream, ITextStream
{
    public override StreamType type => StreamType.Text;

    /// <summary>
    /// Not a subtitle, but a secondary title
    /// </summary>
    public string SubTitle { get; set; }

    /// <summary>
    /// Not From MediaInfo. Is this an external sub file
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// Not from MediaInfo, this is the name of the external sub file
    /// </summary>
    public string Filename { get; set; }

    bool ITextStream.IsExternal => External;
    string ITextStream.ExternalFilename => External && !string.IsNullOrEmpty(Filename) ? Filename : null;

    protected bool Equals(TextStream other) => base.Equals(other) && SubTitle == other.SubTitle && External == other.External && Filename == other.Filename;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((TextStream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ (SubTitle != null ? SubTitle.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ External.GetHashCode();
            hashCode = (hashCode * 397) ^ (Filename != null ? Filename.GetHashCode() : 0);
            return hashCode;
        }
    }
}

[MessagePackObject(true)]
public class MenuStream : Stream
{
    public override StreamType type => StreamType.Menu;

    /// <summary>
    /// Chapters are stored in the format "_hh_mm_ss_fff" : "Chapter Name"
    /// </summary>
    public Dictionary<string, string> extra { get; set; }

    protected bool Equals(MenuStream other) => base.Equals(other) && extra.SequenceEqual(other.extra);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MenuStream)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (base.GetHashCode() * 397) ^ (extra != null ? extra.GetHashCode() : 0);
        }
    }
}

#nullable enable
public class ChapterInfo : IChapterInfo
{
    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public TitleLanguage Language => LanguageCode?.GetTitleLanguage() ?? TitleLanguage.None;

    public string? LanguageCode { get; }

    /// <inheritdoc/>
    public TimeSpan Timestamp { get; }

    public ChapterInfo(string title, string? languageCode, TimeSpan timestamp)
    {
        Title = title;
        LanguageCode = languageCode;
        Timestamp = timestamp;
    }
}

internal class StreamFormatInfoImpl : IStreamFormatInfo
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string? Profile { get; }

    /// <inheritdoc/>
    public string? Level { get; }

    /// <inheritdoc/>
    public string? Settings { get; }

    /// <inheritdoc/>
    public string? AdditionalFeatures { get; }

    /// <inheritdoc/>
    public string? Endianness { get; }

    /// <inheritdoc/>
    public string? Tier { get; }

    /// <inheritdoc/>
    public string? Commercial { get; }

    /// <inheritdoc/>
    public string? HDR { get; }

    /// <inheritdoc/>
    public string? HDRCompatibility { get; }

    /// <inheritdoc/>
    public bool CABAC { get; }

    /// <inheritdoc/>
    public bool BVOP { get; }

    /// <inheritdoc/>
    public bool QPel { get; }

    /// <inheritdoc/>
    public string? GMC { get; }

    /// <inheritdoc/>
    public int? ReferenceFrames { get; }

    public StreamFormatInfoImpl(Stream stream)
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

internal class StreamCodecInfoImpl : IStreamCodecInfo
{
    /// <inheritdoc/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; }

    /// <inheritdoc/>
    public string Simplified { get; }

    /// <inheritdoc/>
    public string? Raw { get; }

    public StreamCodecInfoImpl(Stream stream)
    {
        Name = stream.Codec;
        Simplified = MediaInfoUtility.TranslateCodec(stream) ?? "UNKNOWN";
        Raw = stream.CodecID;
    }
}

internal class StreamMuxingInfoImpl : IStreamMuxingInfo
{
    /// <inheritdoc/>
    public string? Raw { get; }

    public StreamMuxingInfoImpl(VideoStream stream)
    {
        Raw = stream.MuxingMode;
    }
}
