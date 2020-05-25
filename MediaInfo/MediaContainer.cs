using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Shoko.Models.MediaInfo
{
    /// <summary>
    /// This is a properly named Media container class. Names here respect MediaInfo's naming.
    /// Because of this, they can be looked up on https://mediaarea.net/en/MediaInfo/Support/Tags, then converted as needed by specific clients.
    /// If the client is open source, then usually, they already have mappings, as most clients use MediaInfo, anyway.
    /// </summary>
    public class MediaContainer
    {
        public Media media { get; set; }

        // Cache to prevent excessive enumeration on things that will be called A LOT
        private GeneralStream _general { get; set; }
        private VideoStream _video { get; set; }
        private List<AudioStream> _audios { get; set; }
        private List<TextStream> _texts { get; set; }
        private List<MenuStream> _menus { get; set; }

        [JsonIgnore]
        public GeneralStream GeneralStream =>
            _general ?? (_general = media?.track?.FirstOrDefault(a => a?.type == StreamType.General) as GeneralStream);

        [JsonIgnore]
        public VideoStream VideoStream =>
            _video ?? (_video = media?.track?.FirstOrDefault(a => a?.type == StreamType.Video) as VideoStream);

        [JsonIgnore]
        public List<AudioStream> AudioStreams => _audios ?? (_audios =
            media?.track?.Where(a => a?.type == StreamType.Audio)
                .Select(a => a as AudioStream).ToList());

        [JsonIgnore]
        public List<TextStream> TextStreams => _texts ?? (_texts =
            media?.track?.Where(a => a?.type == StreamType.Text)
                .Select(a => a as TextStream).ToList());

        [JsonIgnore]
        public List<MenuStream> MenuStreams => _menus ?? (_menus =
            media?.track?.Where(a => a?.type == StreamType.Menu)
                .Select(a => a as MenuStream).ToList());
    }

    public class Media
    {
        public List<Stream> track { get; set; }
    }

    public enum StreamType
    {
        General,
        Video,
        Audio,
        Text,
        Menu
    }

    public abstract class Stream
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
    }

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
    }

    public class VideoStream : Stream
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
    }

    public class AudioStream : Stream
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

    }

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
    }

    public class TextStream : Stream
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
    }

    public class MenuStream : Stream
    {
        public override StreamType type => StreamType.Menu;

        /// <summary>
        /// Chapters are stored in the format "_hh_mm_ss_fff" : "Chapter Name" 
        /// </summary>
        public Dictionary<string, string> extra { get; set; }
    }
}
