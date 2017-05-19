using System.Threading;
using Shoko.Models.Enums;

namespace Shoko.Commons.Languages
{
    public class EnumTranslator
    {
        public static string EpisodeTypeTranslated(enEpisodeType epType)
        {

            Thread.CurrentThread.CurrentUICulture = Culture.Global;

            switch (epType)
            {
                case enEpisodeType.Credits:
                    return Properties.Resources.EpisodeType_Credits;
                case enEpisodeType.Episode:
                    return Properties.Resources.EpisodeType_Normal;
                case enEpisodeType.Other:
                    return Properties.Resources.EpisodeType_Other;
                case enEpisodeType.Parody:
                    return Properties.Resources.EpisodeType_Parody;
                case enEpisodeType.Special:
                    return Properties.Resources.EpisodeType_Specials;
                case enEpisodeType.Trailer:
                    return Properties.Resources.EpisodeType_Trailer;
                default:
                    return Properties.Resources.EpisodeType_Normal;

            }
        }

        public static enEpisodeType EpisodeTypeTranslatedReverse(string epType)
        {
            Thread.CurrentThread.CurrentUICulture = Culture.Global;

            if (epType == Properties.Resources.EpisodeType_Credits) return enEpisodeType.Credits;
            if (epType == Properties.Resources.EpisodeType_Normal) return enEpisodeType.Episode;
            if (epType == Properties.Resources.EpisodeType_Other) return enEpisodeType.Other;
            if (epType == Properties.Resources.EpisodeType_Parody) return enEpisodeType.Parody;
            if (epType == Properties.Resources.EpisodeType_Trailer) return enEpisodeType.Trailer;
            if (epType == Properties.Resources.EpisodeType_Specials) return enEpisodeType.Special;

            return enEpisodeType.Episode;
        }
    }
}
