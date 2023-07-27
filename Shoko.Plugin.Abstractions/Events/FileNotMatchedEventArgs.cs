
namespace Shoko.Plugin.Abstractions
{
    public class FileNotMatchedEventArgs : FileEventArgs
    {
        /// <summary>
        /// Number of times we've tried to auto-match this file up until now.
        /// </summary>
        public int AutoMatchAttempts { get; set; }

        /// <summary>
        /// True if this file had existing cross-refernces before this match
        /// attempt.
        /// </summary>
        public bool HasCrossReferences { get; set; }

        /// <summary>
        /// True if we're currently UDP banned.
        /// </summary>
        public bool IsUDPBanned { get; set; }
    }
}
