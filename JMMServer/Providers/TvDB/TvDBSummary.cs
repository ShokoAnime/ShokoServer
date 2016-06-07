using System;
using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Providers.TvDB
{
    public class TvDBSummary
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // All the episode overrides for this anime
        private List<CrossRef_AniDB_TvDB_Episode> crossRefTvDBEpisodes;

        // All the TvDB cross refs for this anime
        private List<CrossRef_AniDB_TvDBV2> crossRefTvDBV2;

        private Dictionary<int, int> dictTvDBCrossRefEpisodes;

        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes;

        // TvDB ID
        public Dictionary<int, TvDBDetails> TvDetails = new Dictionary<int, TvDBDetails>();

        public int AnimeID { get; set; }

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

        public Dictionary<int, int> DictTvDBCrossRefEpisodes
        {
            get
            {
                if (dictTvDBCrossRefEpisodes == null)
                {
                    dictTvDBCrossRefEpisodes = new Dictionary<int, int>();
                    foreach (var xrefEp in CrossRefTvDBEpisodes)
                        dictTvDBCrossRefEpisodes[xrefEp.AniDBEpisodeID] = xrefEp.TvDBEpisodeID;
                }
                return dictTvDBCrossRefEpisodes;
            }
        }

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

        private void PopulateCrossRefs()
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                crossRefTvDBV2 = repCrossRef.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void PopulateDictTvDBEpisodes()
        {
            try
            {
                dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                foreach (var det in TvDetails.Values)
                {
                    if (det != null)
                    {
                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        var eps = det.TvDBEpisodes;

                        var i = 1;
                        foreach (var ep in eps)
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
                var repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
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

            foreach (var xref in CrossRefTvDBV2)
            {
                var det = new TvDBDetails(xref.TvDBID);
                TvDetails[xref.TvDBID] = det;
            }
        }
    }
}