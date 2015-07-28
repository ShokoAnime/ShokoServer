using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2SyncCollectionEpisodesByNumber
    {
        [DataMember(Name = "shows")]
        public List<TraktV2ShowCollectedPostByNumber> shows { get; set; }

        public TraktV2SyncCollectionEpisodesByNumber()
        {

        }

        public TraktV2SyncCollectionEpisodesByNumber(string slug, int season, int episodeNumber, DateTime collectedDate)
        {
            shows = new List<TraktV2ShowCollectedPostByNumber>();
            shows.Add(new TraktV2ShowCollectedPostByNumber());
            shows[0].ids = new TraktV2IdsCollectedByNumber();
            shows[0].ids.slug = slug;
            shows[0].seasons = new List<TaktV2SeasonCollectedPostByNumber>();
            shows[0].seasons.Add(new TaktV2SeasonCollectedPostByNumber());
            shows[0].seasons[0].number = season;
            shows[0].seasons[0].episodes = new List<TraktV2EpisodeCollectedPostByNumber>();
            shows[0].seasons[0].episodes.Add(new TraktV2EpisodeCollectedPostByNumber());
            shows[0].seasons[0].episodes[0].number = episodeNumber;
            shows[0].seasons[0].episodes[0].collected_at = collectedDate.ToUniversalTime().ToString("s") + "Z";
        }
    }

    [DataContract]
    public class TraktV2ShowCollectedPostByNumber
    {
        [DataMember(Name = "ids")]
        public TraktV2IdsCollectedByNumber ids { get; set; }

        [DataMember(Name = "seasons")]
        public List<TaktV2SeasonCollectedPostByNumber> seasons { get; set; }
    }

    [DataContract]
    public class TraktV2IdsCollectedByNumber
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }

    [DataContract]
    public class TaktV2SeasonCollectedPostByNumber
    {
        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "episodes")]
        public List<TraktV2EpisodeCollectedPostByNumber> episodes { get; set; }
    }

    [DataContract]
    public class TraktV2EpisodeCollectedPostByNumber
    {
        [DataMember(Name = "collected_at")]
        public string collected_at { get; set; }

        [DataMember(Name = "number")]
        public int number { get; set; }
    }
}
