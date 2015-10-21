using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels.Extensions
{
    public static class AnimeEpisodeExtensions
    {
        //The relation between animeepisode and AniDB_Episode is not 1:1, since movies and ovas could contain multipart episodes
        //But in most cases, we requiere the 1:1 relation.

        public static AniDB_Episode GetAniDB_Episode(this AnimeEpisode ep)
        {
            int minvalu = ep.AniDbEpisodes.Keys.Min();
            return ep.AniDbEpisodes[minvalu].First();
        }
    }
}
