using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.TvDB
{
    public class TvDBDetails
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int TvDBID { get; set; }

        public TvDBDetails(int tvDBID)
        {
            TvDBID = tvDBID;

            PopulateTvDBEpisodes();
        }

        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;

        public Dictionary<int, TvDB_Episode> DictTvDBEpisodes
        {
            get
            {
                if (dictTvDBEpisodes == null)
                {
                    try
                    {
                        if (TvDBEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                            // create a dictionary of absolute episode numbers for tvdb episodes
                            // sort by season and episode number
                            // ignore season 0, which is used for specials
                            List<TvDB_Episode> eps = TvDBEpisodes;


                            int i = 1;
                            foreach (TvDB_Episode ep in eps)
                            {
                                dictTvDBEpisodes[i] = ep;
                                i++;
                            }
                            TimeSpan ts = DateTime.Now - start;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, ex.ToString());
                    }
                }
                return dictTvDBEpisodes;
            }
        }

        private Dictionary<int, int> dictTvDBSeasons = null;

        public Dictionary<int, int> DictTvDBSeasons
        {
            get
            {
                if (dictTvDBSeasons == null)
                {
                    try
                    {
                        if (TvDBEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTvDBSeasons = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            List<TvDB_Episode> eps = TvDBEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (TvDB_Episode ep in eps)
                            {
                                if (ep.SeasonNumber != lastSeason)
                                    dictTvDBSeasons[ep.SeasonNumber] = i;

                                lastSeason = ep.SeasonNumber;
                                i++;
                            }
                            TimeSpan ts = DateTime.Now - start;
                            //logger.Trace("Got TvDB Seasons in {0} ms", ts.TotalMilliseconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, ex.ToString());
                    }
                }
                return dictTvDBSeasons;
            }
        }

        private Dictionary<int, int> dictTvDBSeasonsSpecials = null;

        public Dictionary<int, int> DictTvDBSeasonsSpecials
        {
            get
            {
                if (dictTvDBSeasonsSpecials == null)
                {
                    try
                    {
                        if (TvDBEpisodes != null)
                        {
                            DateTime start = DateTime.Now;

                            dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                            // create a dictionary of season numbers and the first episode for that season

                            List<TvDB_Episode> eps = TvDBEpisodes;
                            int i = 1;
                            int lastSeason = -999;
                            foreach (TvDB_Episode ep in eps)
                            {
                                if (ep.SeasonNumber > 0) continue;

                                int thisSeason = 0;
                                if (ep.AirsBeforeSeason.HasValue) thisSeason = ep.AirsBeforeSeason.Value;
                                if (ep.AirsAfterSeason.HasValue) thisSeason = ep.AirsAfterSeason.Value;

                                if (thisSeason != lastSeason)
                                    dictTvDBSeasonsSpecials[thisSeason] = i;

                                lastSeason = thisSeason;
                                i++;
                            }
                            TimeSpan ts = DateTime.Now - start;
                            //logger.Trace("Got TvDB Seasons in {0} ms", ts.TotalMilliseconds);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, ex.ToString());
                    }
                }
                return dictTvDBSeasonsSpecials;
            }
        }

        private void PopulateTvDBEpisodes()
        {
            try
            {
                tvDBEpisodes = Repo.Instance.TvDB_Episode.GetBySeriesID(TvDBID)
                    .OrderBy(a => a.SeasonNumber)
                    .ThenBy(a => a.EpisodeNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        private List<TvDB_Episode> tvDBEpisodes = null;

        public List<TvDB_Episode> TvDBEpisodes
        {
            get
            {
                if (tvDBEpisodes == null)
                {
                    PopulateTvDBEpisodes();
                }
                return tvDBEpisodes;
            }
        }
    }
}