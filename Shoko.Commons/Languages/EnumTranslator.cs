using System.Threading;
using Shoko.Models.Enums;

namespace Shoko.Commons.Languages
{
    public class EnumTranslator
    {
        public static string EpisodeTypeTranslated(EpisodeType epType)
        {

            Thread.CurrentThread.CurrentUICulture = Culture.Global;

            switch (epType)
            {
                case EpisodeType.Credits:
                    return Properties.Resources.EpisodeType_Credits;
                case EpisodeType.Episode:
                    return Properties.Resources.EpisodeType_Normal;
                case EpisodeType.Other:
                    return Properties.Resources.EpisodeType_Other;
                case EpisodeType.Parody:
                    return Properties.Resources.EpisodeType_Parody;
                case EpisodeType.Special:
                    return Properties.Resources.EpisodeType_Specials;
                case EpisodeType.Trailer:
                    return Properties.Resources.EpisodeType_Trailer;
                default:
                    return Properties.Resources.EpisodeType_Normal;

            }
        }

        public static EpisodeType EpisodeTypeTranslatedReverse(string epType)
        {
            Thread.CurrentThread.CurrentUICulture = Culture.Global;

            if (epType == Properties.Resources.EpisodeType_Credits) return EpisodeType.Credits;
            if (epType == Properties.Resources.EpisodeType_Normal) return EpisodeType.Episode;
            if (epType == Properties.Resources.EpisodeType_Other) return EpisodeType.Other;
            if (epType == Properties.Resources.EpisodeType_Parody) return EpisodeType.Parody;
            if (epType == Properties.Resources.EpisodeType_Trailer) return EpisodeType.Trailer;
            if (epType == Properties.Resources.EpisodeType_Specials) return EpisodeType.Special;

            return EpisodeType.Episode;
        }
    }
}
