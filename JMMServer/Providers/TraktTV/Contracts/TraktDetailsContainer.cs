using System;
using System.Collections.Generic;
using BinaryNorthwest;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Providers.TraktTV
{
    public class TraktDetailsContainer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        private Dictionary<int, Trakt_Episode> dictTraktEpisodes;

        private Dictionary<int, int> dictTraktSeasons;

        private Dictionary<int, int> dictTraktSeasonsSpecials;

        private List<Trakt_Episode> traktEpisodes;

        public TraktDetailsContainer(string traktID)
        {
            TraktID = traktID;

            PopulateTraktDetails();
        }

        public string TraktID { get; set; }
        public Trakt_Show Show { get; set; }

        public Dictionary<int, Trakt_Episode> DictTraktEpisodes
        {
            get
            {
                if (dictTraktEpisodes == null)
                {
                    try
                    {
                        if (TraktEpisodes != null)
                        {
                            var start = DateTime.Now;

                            dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
                            // create a dictionary of absolute episode numbers for Trakt episodes
                            // sort by season and episode number
                            // ignore season 0, which is used for specials
                            var eps = TraktEpisodes;


                            var i = 1;
                            foreach (var ep in eps)
                            {
                                if (ep.EpisodeNumber > 0)
                                {
                                    dictTraktEpisodes[i] = ep;
                                    i++;
                                }
                            }
                            var ts = DateTime.Now - start;
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
                            var start = DateTime.Now;

                            dictTraktSeasons = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            var eps = TraktEpisodes;
                            var i = 1;
                            var lastSeason = -999;
                            foreach (var ep in eps)
                            {
                                if (ep.Season != lastSeason)
                                    dictTraktSeasons[ep.Season] = i;

                                lastSeason = ep.Season;
                                i++;
                            }
                            var ts = DateTime.Now - start;
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
                            var start = DateTime.Now;

                            dictTraktSeasonsSpecials = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            var eps = TraktEpisodes;
                            var i = 1;
                            var lastSeason = -999;
                            foreach (var ep in eps)
                            {
                                if (ep.Season > 0) continue;

                                var thisSeason = 0;

                                if (thisSeason != lastSeason)
                                    dictTraktSeasonsSpecials[thisSeason] = i;

                                lastSeason = thisSeason;
                                i++;
                            }
                            var ts = DateTime.Now - start;
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

        public List<Trakt_Episode> TraktEpisodes
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

        private void PopulateTraktDetails()
        {
            try
            {
                var repShows = new Trakt_ShowRepository();
                Show = repShows.GetByTraktSlug(TraktID);
                if (Show == null) return;

                var repTvEps = new Trakt_EpisodeRepository();
                traktEpisodes = repTvEps.GetByShowID(Show.Trakt_ShowID);

                if (traktEpisodes.Count > 0)
                {
                    var sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("Season", false, SortType.eInteger));
                    sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                    traktEpisodes = Sorting.MultiSort(traktEpisodes, sortCriteria);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}