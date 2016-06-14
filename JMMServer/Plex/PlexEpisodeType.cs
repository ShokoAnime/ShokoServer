using AniDBAPI;

namespace JMMServer.Plex
{
    public class PlexEpisodeType
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public string Image { get; set; }
        public AnimeTypes AnimeType { get; set; }
        public int Count { get; set; }

        public static void EpisodeTypeTranslated(PlexEpisodeType tp, enEpisodeType epType, AnimeTypes an, int cnt)
        {
            tp.Type = (int)epType;
            tp.Count = cnt;
            tp.AnimeType = an;
            bool plural = cnt > 1;
            switch (epType)
            {
                case enEpisodeType.Credits:
                    tp.Name = plural ? "Credits" : "Credit";
                    tp.Image = "plex_credits.png";
                    return;
                case enEpisodeType.Episode:
                    switch (an)
                    {
                        case AnimeTypes.Movie:
                            tp.Name = plural ? "Movies" : "Movie";
                            tp.Image = "plex_movies.png";
                            return;
                        case AnimeTypes.OVA:
                            tp.Name = plural ? "Ovas" : "Ova";
                            tp.Image = "plex_ovas.png";
                            return;
                        case AnimeTypes.Other:
                            tp.Name = plural ? "Others" : "Other";
                            tp.Image = "plex_others.png";
                            return;
                        case AnimeTypes.TV_Series:
                            tp.Name = plural ? "Episodes" : "Episode";
                            tp.Image = "plex_episodes.png";
                            return;
                        case AnimeTypes.TV_Special:
                            tp.Name = plural ? "TV Episodes" : "TV Episode";
                            tp.Image = "plex_tvepisodes.png";
                            return;
                        case AnimeTypes.Web:
                            tp.Name = plural ? "Web Clips" : "Web Clip";
                            tp.Image = "plex_webclips.png";
                            return;
                    }
                    tp.Name = plural ? "Episodes" : "Episode";
                    tp.Image = "plex_episodes.png";
                    return;
                case enEpisodeType.Parody:
                    tp.Name = plural ? "Parodies" : "Parody";
                    tp.Image = "plex_parodies.png";
                    return;
                case enEpisodeType.Special:
                    tp.Name = plural ? "Specials" : "Special";
                    tp.Image = "plex_specials.png";
                    return;
                case enEpisodeType.Trailer:
                    tp.Name = plural ? "Trailers" : "Trailer";
                    tp.Image = "plex_trailers.png";
                    return;
                default:
                    tp.Name = "Misc";
                    tp.Image = "plex_misc.png";
                    return;
            }
        }
    }
}
