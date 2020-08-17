using System.Collections.Generic;

namespace Shoko.Renamer.Abstractions.DataModels
{
    public interface IMediaContainer
    {
        IGeneralStream General { get; }
        IVideoStream Video { get; }
        IList<IAudioStream> Audio { get; }
        IList<ITextStream> Subs { get; }
        bool Chaptered { get; }
    }

    public interface IStream
    {
        string Title { get; set; }

        int StreamOrder { get; set; }

        string Codec { get; set; }
        
        string CodecID { get; set; }
        
        /// <summary>
        /// The Language code (ISO 639-1 in everything I've seen) from MediaInfo
        /// </summary>
        string Language { get; set; }
        
        /// <summary>
        /// This is the 3 character language code
        /// This is mapped from the Language, it is not MediaInfo data
        /// </summary>
        string LanguageCode { get; set; }
        
        /// <summary>
        /// This is the Language Name, "English"
        /// This is mapped from the Language, it is not MediaInfo data
        /// </summary>
        string LanguageName { get; set; }
        
        bool Default { get; set; }
        
        bool Forced { get; set; }
    }

    public interface IGeneralStream : IStream
    {
        double Duration { get; set; }

        int OverallBitRate { get; set; }

        decimal FrameRate { get; set; }
    }

    public interface IVideoStream : IStream
    {
        int BitRate { get; set; }

        int Width { get; set; }

        int Height { get; set; }

        decimal FrameRate { get; set; }

        string FrameRate_Mode { get; set; }

        int BitDepth { get; set; }
    }

    public interface IAudioStream : IStream
    {
        int Channels { get; set; }

        int SamplingRate { get; set; }

        string Compression_Mode { get; set; }

        int BitRate { get; set; }

        string BitRate_Mode { get; set; }

        int BitDepth { get; set; }
    }

    public interface ITextStream : IStream
    {
        /// <summary>
        /// Not From MediaInfo. Is this an external sub file
        /// </summary>
        bool External { get; set; }

        /// <summary>
        /// Not from MediaInfo, this is the name of the external sub file
        /// </summary>
        string Filename { get; set; }
    }
}