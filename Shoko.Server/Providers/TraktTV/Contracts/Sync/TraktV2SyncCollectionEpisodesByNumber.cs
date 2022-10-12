﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2SyncCollectionEpisodesByNumber
{
    [DataMember(Name = "shows")] public List<TraktV2ShowCollectedPostByNumber> shows { get; set; }

    public TraktV2SyncCollectionEpisodesByNumber()
    {
    }

    public TraktV2SyncCollectionEpisodesByNumber(string slug, int season, int episodeNumber, DateTime collectedDate)
    {
        shows = new List<TraktV2ShowCollectedPostByNumber>();
        shows.Add(new TraktV2ShowCollectedPostByNumber());
        shows[0].ids = new TraktV2IdsCollectedByNumber { slug = slug };
        shows[0].seasons = new List<TaktV2SeasonCollectedPostByNumber>();
        shows[0].seasons.Add(new TaktV2SeasonCollectedPostByNumber());
        shows[0].seasons[0].number = season;
        shows[0].seasons[0].episodes = new List<TraktV2EpisodeCollectedPostByNumber>();
        shows[0].seasons[0].episodes.Add(new TraktV2EpisodeCollectedPostByNumber());
        shows[0].seasons[0].episodes[0].number = episodeNumber;
        shows[0].seasons[0].episodes[0].collected_at = collectedDate.ToUniversalTime().ToString("s") + "Z";
    }

    public void AddEpisode(string slug, int season, int episodeNumber, DateTime collectedDate)
    {
        if (shows == null)
        {
            shows = new List<TraktV2ShowCollectedPostByNumber>();
        }

        TraktV2ShowCollectedPostByNumber thisShow = null;
        foreach (var shw in shows)
        {
            if (shw.ids.slug.Equals(slug, StringComparison.InvariantCultureIgnoreCase))
            {
                thisShow = shw;
                break;
            }
        }

        if (thisShow == null)
        {
            thisShow = new TraktV2ShowCollectedPostByNumber
            {
                ids = new TraktV2IdsCollectedByNumber { slug = slug },
                seasons = new List<TaktV2SeasonCollectedPostByNumber>()
            };
            //thisShow.seasons.Add(new TaktV2SeasonCollectedPostByNumber());
            //thisShow.seasons[0].number = season;

            //thisShow.seasons[0].episodes = new List<TraktV2EpisodeCollectedPostByNumber>();

            shows.Add(thisShow);
        }

        TaktV2SeasonCollectedPostByNumber thisSeason = null;
        foreach (var sea in thisShow.seasons)
        {
            if (sea.number == season)
            {
                thisSeason = sea;
                break;
            }
        }

        if (thisSeason == null)
        {
            thisSeason = new TaktV2SeasonCollectedPostByNumber
            {
                number = season, episodes = new List<TraktV2EpisodeCollectedPostByNumber>()
            };
            thisShow.seasons.Add(thisSeason);
        }

        TraktV2EpisodeCollectedPostByNumber thisEp = null;
        foreach (var ep in thisSeason.episodes)
        {
            if (ep.number == episodeNumber)
            {
                thisEp = ep;
                break;
            }
        }

        if (thisEp == null)
        {
            thisEp = new TraktV2EpisodeCollectedPostByNumber
            {
                number = episodeNumber, collected_at = collectedDate.ToUniversalTime().ToString("s") + "Z"
            };
            thisSeason.episodes.Add(thisEp);
        }
    }
}

[DataContract]
public class TraktV2ShowCollectedPostByNumber
{
    [DataMember(Name = "ids")] public TraktV2IdsCollectedByNumber ids { get; set; }

    [DataMember(Name = "seasons")] public List<TaktV2SeasonCollectedPostByNumber> seasons { get; set; }

    public override string ToString()
    {
        return ids != null
            ? string.Format("{0}", ids.slug)
            : string.Empty;
    }
}

[DataContract]
public class TraktV2IdsCollectedByNumber
{
    [DataMember(Name = "slug")] public string slug { get; set; }

    public override string ToString()
    {
        return slug;
    }
}

[DataContract]
public class TaktV2SeasonCollectedPostByNumber
{
    [DataMember(Name = "number")] public int number { get; set; }

    [DataMember(Name = "episodes")] public List<TraktV2EpisodeCollectedPostByNumber> episodes { get; set; }

    public override string ToString()
    {
        return string.Format("S{0}", number);
    }
}

[DataContract]
public class TraktV2EpisodeCollectedPostByNumber
{
    [DataMember(Name = "collected_at")] public string collected_at { get; set; }

    [DataMember(Name = "number")] public int number { get; set; }

    public override string ToString()
    {
        return string.Format("EP-{0} : {1}", number, collected_at);
    }
}
