using System;
using System.Collections.Generic;
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
        public List<Stream> track { get; set; }
    }

    public abstract class Stream
    {
        public int ID { get; set; }
        
        public abstract string type { get; }
        
        public string Title { get; set; }

        public int StreamOrder { get; set; }

        public string Format { get; set; }
        
        public string Format_Profile { get; set; }
        
        public string Format_Level { get; set; }
        
        public string Format_Commercial_IfAny { get; set; }
        
        public string Format_Tier { get; set; }
        
        public string Format_AdditionalFeatures { get; set; }
        
        public string Format_Settings_Endianness { get; set; }
        
        public string CodecID { get; set; }
        
        public string Language { get; set; }
        
        public bool Default { get; set; }
        
        public bool Forced { get; set; }
    }

    public class GeneralStream : Stream
    {
        public override string type => "General";

        public int OverallBitRate { get; set; }
        
        public int Format_Version { get; set; }
        
        public decimal FrameRate { get; set; }
        
        public bool IsStreamable { get; set; }
        
        public DateTime? Encoded_Date { get; set; }
    }

    public class VideoStream : Stream
    {
        public override string type => "Video";

        public bool? Format_Settings_CABAC { get; set; }
            
        public int? Format_Settings_RefFrames { get; set; }
        
        public int BitRate { get; set; }
        
        public int Width { get; set; }
        
        public int Height { get; set; }
        
        public decimal? PixelAspectRatio { get; set; }
        
        public decimal? FrameRate { get; set; }
        
        public string FrameRate_Mode { get; set; }
        
        public int FrameCount { get; set; }
        
        public string ColorSpace { get; set; }
        
        public string ChromaSubsampling { get; set; }
        
        public int BitDepth { get; set; }
        
        public string ScanType { get; set; }
        
        public string Encoded_Library_Name { get; set; }
        
        // HDR stuff. Can be on SD, but not as common, and not very useful
        
        public string colour_range { get; set; }
        
        public string colour_primaries { get; set; }
        
        public string transfer_characteristics { get; set; }
        
        public string matrix_coefficients { get; set; }
        
        public VideoExtra extra { get; set; }
    }

    public class VideoExtra
    {
        // Video
        public string MasteringDisplay_ColorPrimaries { get; set; }
        
        public string MasteringDisplay_Luminance { get; set; }
        
        public string MaxCLL { get; set; }
        
        public string MaxFALL { get; set; }
    }

    public class AudioStream : Stream
    {
        public override string type => "Audio";
        
        public int Channels { get; set; }
        
        public string ChannelLayout { get; set; }
        
        public int SamplesPerFrame { get; set; }
        
        public int SamplingRate { get; set; }
        
        public string Compression_Mode { get; set; }
        
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
        public override string type => "Text";
    }

    public class ChapterStream : Stream
    {
        public override string type => "Menu";
        
        /// <summary>
        /// Chapters are stored in the format "_hh_mm_ss_fff" : "Chapter Name" 
        /// </summary>
        public Dictionary<string, string> extra { get; set; }
    }
}