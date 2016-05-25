﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;

namespace JMMServer.Providers.TvDB
{
    public class TvDBSummary
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int AnimeID { get; set; }

        // TvDB ID
        public Dictionary<int, TvDBDetails> TvDetails = new Dictionary<int, TvDBDetails>();

        // All the TvDB cross refs for this anime
        private List<CrossRef_AniDB_TvDBV2> crossRefTvDBV2 = null;
        public List<CrossRef_AniDB_TvDBV2> CrossRefTvDBV2
        {
            get
            {
                if (crossRefTvDBV2 == null)
                {
                    PopulateCrossRefs();
                }
                return crossRefTvDBV2;
            }
        }

        private void PopulateCrossRefs()
        {
            try
            {
                CrossRef_AniDB_TvDBV2Repository repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                crossRefTvDBV2 = repCrossRef.GetByAnimeID(AnimeID);
              
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        // All the episode overrides for this anime
        private List<CrossRef_AniDB_TvDB_Episode> crossRefTvDBEpisodes = null;
        public List<CrossRef_AniDB_TvDB_Episode> CrossRefTvDBEpisodes
        {
            get
            {
                if (crossRefTvDBEpisodes == null)
                {
                    PopulateCrossRefsEpisodes();
                }
                return crossRefTvDBEpisodes;
            }
        }

        private Dictionary<int, int> dictTvDBCrossRefEpisodes = null;
        public Dictionary<int, int> DictTvDBCrossRefEpisodes
        {
            get
            {
                if (dictTvDBCrossRefEpisodes == null)
                {
                    dictTvDBCrossRefEpisodes = new Dictionary<int, int>();
                    foreach (CrossRef_AniDB_TvDB_Episode xrefEp in CrossRefTvDBEpisodes)
                        dictTvDBCrossRefEpisodes[xrefEp.AniDBEpisodeID] = xrefEp.TvDBEpisodeID;
                }
                return dictTvDBCrossRefEpisodes;
            }
        }

        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;
        public Dictionary<int, TvDB_Episode> DictTvDBEpisodes
        {
            get
            {
                if (dictTvDBEpisodes == null)
                {
                    PopulateDictTvDBEpisodes();
                }
                return dictTvDBEpisodes;
            }
        }

        private void PopulateDictTvDBEpisodes()
        {
            try
            {
                dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                foreach (TvDBDetails det in TvDetails.Values)
                {
                    if (det != null)
                    {

                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        List<TvDB_Episode> eps = det.TvDBEpisodes;

                        int i = 1;
                        foreach (TvDB_Episode ep in eps)
                        {
                            dictTvDBEpisodes[i] = ep;
                            i++;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void PopulateCrossRefsEpisodes()
        {
            try
            {
                CrossRef_AniDB_TvDB_EpisodeRepository repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
                crossRefTvDBEpisodes = repCrossRef.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void Populate(int animeID)
        {
            AnimeID = animeID;

            try
            {
                PopulateCrossRefs();
                PopulateCrossRefsEpisodes();
                PopulateTvDBDetails();
                PopulateDictTvDBEpisodes();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void PopulateTvDBDetails()
        {
            if (CrossRefTvDBV2 == null) return;

            foreach (CrossRef_AniDB_TvDBV2 xref in CrossRefTvDBV2)
            {
                TvDBDetails det = new TvDBDetails(xref.TvDBID);
                TvDetails[xref.TvDBID] = det;
            }
        }

    }
}
