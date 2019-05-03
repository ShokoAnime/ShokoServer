using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3
{
    public static class ModelHelper
    {
        public static Sizes GenerateSizes(List<SVR_AnimeEpisode> ael, int uid)
        {
            int eps = 0;
            int credits = 0;
            int specials = 0;
            int trailers = 0;
            int parodies = 0;
            int others = 0;

            int localEps = 0;
            int localCredits = 0;
            int localSpecials = 0;
            int localTrailers = 0;
            int localParodies = 0;
            int localOthers = 0;

            int watchedEps = 0;
            int watchedCredits = 0;
            int watchedSpecials = 0;
            int watchedTrailers = 0;
            int watchedParodies = 0;
            int watchedOthers = 0;

            // single loop. Will help on long shows
            foreach (SVR_AnimeEpisode ep in ael)
            {
                if (ep?.AniDB_Episode == null) continue;
                var local = ep.GetVideoLocals().Any();
                bool watched = (ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0;
                switch (ep.EpisodeTypeEnum)
                {
                    case EpisodeType.Episode:
                    {
                        eps++;
                        if (local) localEps++;
                        if (watched) watchedEps++;
                        break;
                    }
                    case EpisodeType.Credits:
                    {
                        credits++;
                        if (local) localCredits++;
                        if (watched) watchedCredits++;
                        break;
                    }
                    case EpisodeType.Special:
                    {
                        specials++;
                        if (local) localSpecials++;
                        if (watched) watchedSpecials++;
                        break;
                    }
                    case EpisodeType.Trailer:
                    {
                        trailers++;
                        if (local) localTrailers++;
                        if (watched) watchedTrailers++;
                        break;
                    }
                    case EpisodeType.Parody:
                    {
                        parodies++;
                        if (local) localParodies++;
                        if (watched) watchedParodies++;
                        break;
                    }
                    case EpisodeType.Other:
                    {
                        others++;
                        if (local) localOthers++;
                        if (watched) watchedOthers++;
                        break;
                    }
                }
            }

            Sizes s = new Sizes
            {
                Total =
                    new Sizes.EpisodeCounts()
                    {
                        Episodes = eps,
                        Credits = credits,
                        Specials = specials,
                        Trailers = trailers,
                        Parodies = parodies,
                        Others = others
                    },
                Local = new Sizes.EpisodeCounts()
                {
                    Episodes = localEps,
                    Credits = localCredits,
                    Specials = localSpecials,
                    Trailers = localTrailers,
                    Parodies = localParodies,
                    Others = localOthers
                },
                Watched = new Sizes.EpisodeCounts()
                {
                    Episodes = watchedEps,
                    Credits = watchedCredits,
                    Specials = watchedSpecials,
                    Trailers = watchedTrailers,
                    Parodies = watchedParodies,
                    Others = watchedOthers
                }
            };
            return s;
        }
    }
}