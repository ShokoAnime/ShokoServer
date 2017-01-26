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

        public static string TorrentSourceTranslated(TorrentSourceType tsType)
        {
            switch (tsType)
            {
                case TorrentSourceType.TokyoToshokanAnime: return "Tokyo Toshokan (Anime)";
                case TorrentSourceType.TokyoToshokanAll: return "Tokyo Toshokan (All)";
                case TorrentSourceType.BakaBT: return "BakaBT";
                case TorrentSourceType.Nyaa: return "Nyaa";
                case TorrentSourceType.Sukebei: return "Sukebei Nyaa";
                case TorrentSourceType.AnimeBytes: return "AnimeBytes";
                default: return "Tokyo Toshokan (Anime)";
            }
        }

        public static string TorrentSourceTranslatedShort(TorrentSourceType tsType)
        {
            switch (tsType)
            {
                case TorrentSourceType.TokyoToshokanAnime: return "TT";
                case TorrentSourceType.TokyoToshokanAll: return "TT";
                case TorrentSourceType.BakaBT: return "BakaBT";
                case TorrentSourceType.Nyaa: return "Nyaa";
                case TorrentSourceType.Sukebei: return "SukeNyaa";
                case TorrentSourceType.AnimeBytes: return "AByt.es";
                default: return "TT";
            }
        }

        public static TorrentSourceType TorrentSourceTranslatedReverse(string tsType)
        {
            if (tsType == "Tokyo Toshokan (Anime)") return TorrentSourceType.TokyoToshokanAnime;
            if (tsType == "Tokyo Toshokan (All)") return TorrentSourceType.TokyoToshokanAll;
            if (tsType == "BakaBT") return TorrentSourceType.BakaBT;
            if (tsType == "Nyaa") return TorrentSourceType.Nyaa;
            if (tsType == "Sukebei Nyaa") return TorrentSourceType.Sukebei;
            if (tsType == "AnimeBytes") return TorrentSourceType.AnimeBytes;

            return TorrentSourceType.TokyoToshokanAnime;
        }
    }
}
