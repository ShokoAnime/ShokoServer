namespace Shoko.Renamer.Abstractions.DataModels
{
    public interface IVideo
    {
        /// <summary>
        /// The name of the file being renamed or moved, before any actions are applied
        /// </summary>
        string Filename { get; set; }

        /// <summary>
        /// The Absolute Path of the file being moved or renamed.
        /// </summary>
        string FilePath { get; set; }

        IMediaContainer MediaInfo { get; }
    }
}