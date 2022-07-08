using System;

namespace Shoko.Plugin.Abstractions
{

    public class AniDBBannedEventArgs
    {
        /// <summary>
        /// Type of ban
        /// </summary>
        public AniDBBanType Type { get; set; }
        /// <summary>
        /// The time the ban occurred. It should be basically "now"
        /// </summary>
        public DateTime Time { get; set; }
        /// <summary>
        /// The time when Shoko will attempt again. This time is just guessed. We get no data or hint of any kind for this value to prevent additional bans.
        /// </summary>
        public DateTime ResumeTime { get; set; }
    }

    public enum AniDBBanType
    {
        UDP,
        HTTP,
    }
}