using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts.Sync
{
    [DataContract]
    public class TraktV2SyncWatchedEpisodesByNumber
    {
        [DataMember(Name = "shows")]
        public List<TraktV2ShowWatchedPostByNumber> shows { get; set; }

        public TraktV2SyncWatchedEpisodesByNumber()
        {
        }

        public TraktV2SyncWatchedEpisodesByNumber(string slug, int season, int episodeNumber, DateTime watchedDate)
        {
            shows = new List<TraktV2ShowWatchedPostByNumber>();
            shows.Add(new TraktV2ShowWatchedPostByNumber());
            shows[0].ids = new TraktV2IdsWatchedByNumber
            {
                slug = slug
            };
            shows[0].seasons = new List<TaktV2SeasonWatchedPostByNumber>();
            shows[0].seasons.Add(new TaktV2SeasonWatchedPostByNumber());
            shows[0].seasons[0].number = season;
            shows[0].seasons[0].episodes = new List<TraktV2EpisodeWatchedPostByNumber>();
            shows[0].seasons[0].episodes.Add(new TraktV2EpisodeWatchedPostByNumber());
            shows[0].seasons[0].episodes[0].number = episodeNumber;
            shows[0].seasons[0].episodes[0].watched_at = watchedDate.ToUniversalTime().ToString("s") + "Z";
        }

        public void AddEpisode(string slug, int season, int episodeNumber, DateTime watchedDate)
        {
            if (shows == null)
                shows = new List<TraktV2ShowWatchedPostByNumber>();

            TraktV2ShowWatchedPostByNumber thisShow = null;
            foreach (TraktV2ShowWatchedPostByNumber shw in shows)
            {
                if (shw.ids.slug.Equals(slug, StringComparison.InvariantCultureIgnoreCase))
                {
                    thisShow = shw;
                    break;
                }
            }
            if (thisShow == null)
            {
                thisShow = new TraktV2ShowWatchedPostByNumber
                {
                    ids = new TraktV2IdsWatchedByNumber
                    {
                        slug = slug
                    },

                    seasons = new List<TaktV2SeasonWatchedPostByNumber>()
                };
                //thisShow.seasons.Add(new TaktV2SeasonWatchedPostByNumber());
                //thisShow.seasons[0].number = season;

                //thisShow.seasons[0].episodes = new List<TraktV2EpisodeWatchedPostByNumber>();

                shows.Add(thisShow);
            }

            TaktV2SeasonWatchedPostByNumber thisSeason = null;
            foreach (TaktV2SeasonWatchedPostByNumber sea in thisShow.seasons)
            {
                if (sea.number == season)
                {
                    thisSeason = sea;
                    break;
                }
            }
            if (thisSeason == null)
            {
                thisSeason = new TaktV2SeasonWatchedPostByNumber
                {
                    number = season,
                    episodes = new List<TraktV2EpisodeWatchedPostByNumber>()
                };
                thisShow.seasons.Add(thisSeason);
            }

            TraktV2EpisodeWatchedPostByNumber thisEp = null;
            foreach (TraktV2EpisodeWatchedPostByNumber ep in thisSeason.episodes)
            {
                if (ep.number == episodeNumber)
                {
                    thisEp = ep;
                    break;
                }
            }
            if (thisEp == null)
            {
                thisEp = new TraktV2EpisodeWatchedPostByNumber
                {
                    number = episodeNumber,
                    watched_at = watchedDate.ToUniversalTime().ToString("s") + "Z"
                };
                thisSeason.episodes.Add(thisEp);
            }
        }
    }

    [DataContract]
    public class TraktV2ShowWatchedPostByNumber
    {
        [DataMember(Name = "ids")]
        public TraktV2IdsWatchedByNumber ids { get; set; }

        [DataMember(Name = "seasons")]
        public List<TaktV2SeasonWatchedPostByNumber> seasons { get; set; }
    }

    [DataContract]
    public class TraktV2IdsWatchedByNumber
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }

    [DataContract]
    public class TraktV2EpisodeWatchedPostByNumber
    {
        [DataMember(Name = "watched_at")]
        public string watched_at { get; set; }

        [DataMember(Name = "number")]
        public int number { get; set; }
    }

    [DataContract]
    public class TaktV2SeasonWatchedPostByNumber
    {
        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "episodes")]
        public List<TraktV2EpisodeWatchedPostByNumber> episodes { get; set; }
    }
}