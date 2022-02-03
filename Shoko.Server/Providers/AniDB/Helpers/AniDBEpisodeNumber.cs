using System;

namespace Shoko.Server.Providers.AniDB.Helpers
{
    public class AniDBEpisodeNumber
    {
        /// <summary>
        /// The episode type, special,etc
        /// </summary>
        public EpisodeType EpisodeType { get; set; }

        /// <summary>
        /// Episode Number
        /// </summary>
        public int EpisodeNumber { get; set; }

        public override string ToString()
        {
            string prefix;
            switch (EpisodeType)
            {
                case EpisodeType.Episode:
                    prefix = string.Empty;
                    break;
                case EpisodeType.Special:
                    prefix = "S";
                    break;
                case EpisodeType.Credits:
                    prefix = "C";
                    break;
                case EpisodeType.Trailer:
                    prefix = "T";
                    break;
                case EpisodeType.Parody:
                    prefix = "P";
                    break;
                case EpisodeType.Other:
                    prefix = "O";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return prefix + EpisodeNumber;
        }
    }
}
