using AniDBAPI;
using Shoko.Models;
using Shoko.Models.Enums;

namespace Shoko.Server.PlexAndKodi
{
    public class PlexEpisodeType
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public string Image { get; set; }
        public AnimeType AnimeType { get; set; }
        public int Count { get; set; }

        public static void EpisodeTypeTranslated(PlexEpisodeType tp, EpisodeType epType, AnimeType an, int cnt)
        {
            tp.Type = (int) epType;
            tp.Count = cnt;
            tp.AnimeType = an;
            bool plural = cnt > 1;
            switch (epType)
            {
                case EpisodeType.Credits:
                    tp.Name = plural ? "Credits" : "Credit";
                    tp.Image = "plex_credits.png";
                    return;
                case EpisodeType.Episode:
                    switch (an)
                    {
                        case AnimeType.Movie:
                            tp.Name = plural ? "Movies" : "Movie";
                            tp.Image = "plex_movies.png";
                            return;
                        case AnimeType.OVA:
                            tp.Name = plural ? "Ovas" : "Ova";
                            tp.Image = "plex_ovas.png";
                            return;
                        case AnimeType.Other:
                            tp.Name = plural ? "Others" : "Other";
                            tp.Image = "plex_others.png";
                            return;
                        case AnimeType.TVSeries:
                            tp.Name = plural ? "Episodes" : "Episode";
                            tp.Image = "plex_episodes.png";
                            return;
                        case AnimeType.TVSpecial:
                            tp.Name = plural ? "TV Episodes" : "TV Episode";
                            tp.Image = "plex_tvepisodes.png";
                            return;
                        case AnimeType.Web:
                            tp.Name = plural ? "Web Clips" : "Web Clip";
                            tp.Image = "plex_webclips.png";
                            return;
                    }
                    tp.Name = plural ? "Episodes" : "Episode";
                    tp.Image = "plex_episodes.png";
                    return;
                case EpisodeType.Parody:
                    tp.Name = plural ? "Parodies" : "Parody";
                    tp.Image = "plex_parodies.png";
                    return;
                case EpisodeType.Special:
                    tp.Name = plural ? "Specials" : "Special";
                    tp.Image = "plex_specials.png";
                    return;
                case EpisodeType.Trailer:
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