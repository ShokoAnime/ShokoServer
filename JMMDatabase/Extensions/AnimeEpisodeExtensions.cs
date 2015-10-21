using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels;
using JMMModels.Childs;
using Raven.Abstractions.Extensions;

namespace JMMDatabase.Extensions
{
    public static class AnimeEpisodeExtensions
    {
        public static void ToggleWatchedStatus(this AnimeEpisode ep, bool watched, bool updateOnline, DateTime? watchedDate, JMMUser user, bool syncTrakt)
        {
            ep.AniDbEpisodes.SelectMany(a=>a.Value).SelectMany(a=>a.VideoLocals).ForEach(a=>a.ToggleWatchedStatus(watched,updateOnline,watchedDate,user,syncTrakt,true));
        }
    }
}
