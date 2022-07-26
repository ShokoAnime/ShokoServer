using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Helpers
{
    public static class ModelHelper
    {
        public static ListResult<T> ToListResult<T>(this IEnumerable<T> enumerable, int page, int pageSize)
        {
            var total = enumerable.Count();
            if (pageSize <= 0)
                return new ListResult<T>
                {
                    Total = total,
                    List = enumerable
                        .ToList(),
                };
            return new ListResult<T>
            {
                Total = total,
                List = enumerable
                    .Skip(pageSize * (page - 1))
                    .Take(pageSize)
                    .ToList(),
            };
        }

        public static ListResult<U> ToListResult<T, U>(this IEnumerable<T> enumerable, Func<T, U> mapper, int page, int pageSize)
        {
            var total = enumerable.Count();
            if (pageSize <= 0)
                return new ListResult<U>
                {
                    Total = total,
                    List = enumerable
                        .Select(mapper)
                        .ToList(),
                };
            return new ListResult<U>
            {
                Total = total,
                List = enumerable
                    .Skip(pageSize * (page - 1))
                    .Take(pageSize)
                    .Select(mapper)
                    .ToList(),
            };
        }
        
        public static (int, EpisodeType?, string) GetEpisodeNumberAndTypeFromInput(string input)
        {
            EpisodeType? episodeType = null;
            if (!int.TryParse(input, out int episodeNumber))
            {
                var maybeType = input[0];
                var maybeRangeStart = input.Substring(1);
                if (!int.TryParse(maybeRangeStart, out episodeNumber))
                    return (0, null, "Unable to parse an int from `{VariableName}`");

                episodeType = maybeType switch
                {
                    'S' => EpisodeType.Special,
                    'C' => EpisodeType.Credits,
                    'T' => EpisodeType.Trailer,
                    'P' => EpisodeType.Parody,
                    'O' => EpisodeType.Other,
                    'E' => EpisodeType.Episode,
                    _ => null,
                };
                if (!episodeType.HasValue) {
                    return (0, null, $"Unknown episode type '{maybeType}' number in `{{VariableName}}`.");
                }
            }
            return (episodeNumber, episodeType, null);
        }
        
        public static int GetTotalEpisodesForType(List<SVR_AnimeEpisode> ael, EpisodeType episodeType)
        {
            var sizes = Helpers.ModelHelper.GenerateSizes(ael, 0);
            return episodeType switch
            {
                EpisodeType.Episode => sizes.Total.Episodes,
                EpisodeType.Special => sizes.Total.Specials,
                EpisodeType.Credits => sizes.Total.Credits,
                EpisodeType.Trailer => sizes.Total.Trailers,
                EpisodeType.Parody => sizes.Total.Parodies,
                EpisodeType.Other => sizes.Total.Others,
                _ => 0,
            };
        }

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
                    new Sizes.EpisodeCounts
                    {
                        Episodes = eps,
                        Credits = credits,
                        Specials = specials,
                        Trailers = trailers,
                        Parodies = parodies,
                        Others = others
                    },
                Local = new Sizes.EpisodeCounts
                {
                    Episodes = localEps,
                    Credits = localCredits,
                    Specials = localSpecials,
                    Trailers = localTrailers,
                    Parodies = localParodies,
                    Others = localOthers
                },
                Watched = new Sizes.EpisodeCounts
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