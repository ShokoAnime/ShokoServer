using System;
using System.Collections.Generic;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Shoko.Models.MediaInfo
{
    /// <summary>
    /// This is a properly named Media container class, modelled after MediaInfo, rather than Plex. Names here respect MediaInfo's naming.
    /// </summary>
    public class MediaContainer
    {
        public List<Track> Track { get; set; }
    }

    /// <summary>
    /// A track, the uppermost part of the hierarchy. There's basically always only one, but it supports more
    /// </summary>
    public class Track
    {
        public List<Stream> Streams { get; set; }
    }

    public abstract class Stream
    {
        public int ID { get; set; }
        
        public abstract string type { get; }

        public int StreamOrder { get; set; }

        public string Format { get; set; }
        
        public string CodecID { get; set; }
        
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
        
        public string Format_Profile { get; set; }
        
        public string Format_Level { get; set; }
        
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
        
        public string colour_range { get; set; }
        
        public string colour_primaries { get; set; }
        
        public string transfer_characteristics { get; set; }
        
        public string matrix_coefficients { get; set; }
        
        /*
          
         */
    }

    public class AudioStream : Stream
    {
        public override string type => "Audio";
    }

    public class TextStream : Stream
    {
        public override string type => "Text";
    }

    public class ChapterStream : Stream
    {
        public override string type => "Menu";
    }
}