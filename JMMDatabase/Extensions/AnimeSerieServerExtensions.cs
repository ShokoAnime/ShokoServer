using System.Collections.Generic;
using System.Linq;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Extensions
{
    public static class AnimeSerieServerExtensions
    {
        public static VideoLocal VideoLocalFromHash(this AnimeSerie s, string hash)
        {
            return s.Episodes.SelectMany(a => a.AniDbEpisodes).SelectMany(a => a.Value).SelectMany(a => a.VideoLocals).FirstOrDefault(a=>a.Hash==hash);
        }

        public static List<AniDB_Episode> AniDB_EpisodesFromVideoLocal(this AnimeSerie s, VideoLocal vl)
        {
            return s.Episodes.SelectMany(b=>b.AniDbEpisodes).SelectMany(d=>d.Value).Where(a=>a.VideoLocals.Any(c=>c.Hash==vl.Hash)).ToList();
        }

        public static AnimeEpisode AnimeEpisodeFromAniDB_Episode(this AnimeSerie s, AniDB_Episode ep)
        {
            return s.Episodes.FirstOrDefault(b => b.AniDbEpisodes.SelectMany(a=>a.Value).Any(a=>a.Id==ep.Id));
        }
    }
}
