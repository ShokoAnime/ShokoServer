using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using NLog;

namespace JMMServer.Providers.TraktTV
{
    public class TraktDetailsContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string TraktID { get; set; }
        public Trakt_Show Show { get; set; }

        public TraktDetailsContainer(string traktID)
        {
            TraktID = traktID;

            PopulateTraktDetails();
        }


        private Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;

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
                            DateTime start = DateTime.Now;

                            dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
                            // create a dictionary of absolute episode numbers for Trakt episodes
                            // sort by season and episode number
                            // ignore season 0, which is used for specials
                            List<Trakt_Episode> eps = TraktEpisodes;


                            int i = 1;
                            foreach (Trakt_Episode ep in eps)
                            {
                                if (ep.EpisodeNumber > 0)
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
                        logger.Error( ex,ex.ToString());
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

                            List<Trakt_Episode> eps = TraktEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (Trakt_Episode ep in eps)
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
                        logger.Error( ex,ex.ToString());
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

                            List<Trakt_Episode> eps = TraktEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (Trakt_Episode ep in eps)
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
                        logger.Error( ex,ex.ToString());
                    }
                }
                return dictTraktSeasonsSpecials;
            }
        }

        private void PopulateTraktDetails()
        {
            try
            {
                Show = RepoFactory.Trakt_Show.GetByTraktSlug(TraktID);
                if (Show == null) return;

                traktEpisodes = RepoFactory.Trakt_Episode.GetByShowID(Show.Trakt_ShowID).OrderBy(a=>a.Season).ThenBy(a=>a.EpisodeNumber).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        private List<Trakt_Episode> traktEpisodes = null;

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
    }
}