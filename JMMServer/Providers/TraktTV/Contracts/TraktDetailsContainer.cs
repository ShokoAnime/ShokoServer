using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using JMMServer.Repositories;
using BinaryNorthwest;
using JMMModels;
using JMMModels.Childs;
using NLog;

namespace JMMServer.Providers.TraktTV
{
    public class TraktDetailsContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public AnimeSerie Serie { get; set; }
        public AniDB_Anime_Trakt Trakt { get; set; }
        public Trakt_Show Show { get; set; }

        public TraktDetailsContainer(AnimeSerie serie, AniDB_Anime_Trakt trakt)
        {
            Trakt = trakt;
            Serie = serie;
            PopulateTraktDetails();
        }



        private Dictionary<int, Episode_TraktEpisode> dictTraktEpisodes = null;
        public Dictionary<int, Episode_TraktEpisode> DictTraktEpisodes
        {
            get
            {
                if (dictTraktEpisodes == null)
                {
                    try
                    {
                        if (TraktEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTraktEpisodes = new Dictionary<int, Episode_TraktEpisode>();
                            // create a dictionary of absolute episode numbers for Trakt episodes
                            // sort by season and episode number
                            // ignore season 0, which is used for specials
                            List<Episode_TraktEpisode> eps = TraktEpisodes;


                            int i = 1;
                            foreach (Episode_TraktEpisode ep in eps)
                            {
                                if (ep.Number > 0)
                                {
                                    dictTraktEpisodes[i] = ep;
                                    i++;
                                }

                            }
                            TimeSpan ts = DateTime.Now - start;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }
                }
                return dictTraktEpisodes;
            }
        }

        private Dictionary<int, int> dictTraktSeasons = null;
        public Dictionary<int, int> DictTraktSeasons
        {
            get
            {
                if (dictTraktSeasons == null)
                {
                    try
                    {
                        if (TraktEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTraktSeasons = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            List<Episode_TraktEpisode> eps = TraktEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (Episode_TraktEpisode ep in eps)
                            {
                                if (ep.Season != lastSeason)
                                    dictTraktSeasons[ep.Season] = i;

                                lastSeason = ep.Season;
                                i++;

                            }
                            TimeSpan ts = DateTime.Now - start;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }
                }
                return dictTraktSeasons;
            }
        }

        private Dictionary<int, int> dictTraktSeasonsSpecials = null;
        public Dictionary<int, int> DictTraktSeasonsSpecials
        {
            get
            {
                if (dictTraktSeasonsSpecials == null)
                {
                    try
                    {
                        if (TraktEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTraktSeasonsSpecials = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            List<Episode_TraktEpisode> eps = TraktEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (Episode_TraktEpisode ep in eps)
                            {
                                if (ep.Season > 0) continue;

                                int thisSeason = 0;

                                if (thisSeason != lastSeason)
                                    dictTraktSeasonsSpecials[thisSeason] = i;

                                lastSeason = thisSeason;
                                i++;

                            }
                            TimeSpan ts = DateTime.Now - start;
                            //logger.Trace("Got TvDB Seasons in {0} ms", ts.TotalMilliseconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException(ex.ToString(), ex);
                    }
                }
                return dictTraktSeasonsSpecials;
            }
        }

        private void PopulateTraktDetails()
        {
            try
            {
                traktEpisodes=Serie.Episodes.Where(a => a.TraktEpisode != null && a.TraktEpisode.ShowId == Trakt.TraktId).Select(a=>a.TraktEpisode).ToList();
                if (traktEpisodes.Count > 0)
                {
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("Season", false, SortType.eInteger));
                    sortCriteria.Add(new SortPropOrFieldAndDirection("Number", false, SortType.eInteger));
                    traktEpisodes = Sorting.MultiSort<Episode_TraktEpisode>(traktEpisodes, sortCriteria);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private List<Episode_TraktEpisode> traktEpisodes = null;
        public List<Episode_TraktEpisode> TraktEpisodes
        {
            get
            {
                if (traktEpisodes == null)
                {
                    PopulateTraktDetails();
                }
                return traktEpisodes;
            }
        }
    }
}
