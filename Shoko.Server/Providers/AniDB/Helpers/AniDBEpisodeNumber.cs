using Shoko.Models.Enums;

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

            }

            return EpisodeNumber.ToString();
        }
    }
}
