using System;
using System.Collections.Generic;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.TvDB
{
    public class TvDBSummary
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int AnimeID { get; set; }

        // TvDB ID
        public Dictionary<int, TvDBDetails> TvDetails = new Dictionary<int, TvDBDetails>();

        // All the TvDB cross refs for this anime
        private List<CrossRef_AniDB_TvDB> crossRefTvDB = null;

        public List<CrossRef_AniDB_TvDB> CrossRefTvDB
        {
            get
            {
                if (crossRefTvDB == null)
                {
                    PopulateCrossRefs();
                }
                return crossRefTvDB;
            }
        }

        private void PopulateCrossRefs()
        {
            try
            {
                crossRefTvDB = Repo.Instance.CrossRef_AniDB_TvDB.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        // All the episode overrides for this anime
        private List<CrossRef_AniDB_TvDB_Episode_Override> crossRefTvDBEpisodes = null;

        public List<CrossRef_AniDB_TvDB_Episode_Override> CrossRefTvDBEpisodes
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
                    foreach (CrossRef_AniDB_TvDB_Episode_Override xrefEp in CrossRefTvDBEpisodes)
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
                logger.Error(ex, ex.ToString());
            }
        }

        private void PopulateCrossRefsEpisodes()
        {
            try
            {
                crossRefTvDBEpisodes = Repo.Instance.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
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
                logger.Error(ex, ex.ToString());
            }
        }

        private void PopulateTvDBDetails()
        {
            if (CrossRefTvDB == null) return;

            foreach (CrossRef_AniDB_TvDB xref in CrossRefTvDB)
            {
                TvDBDetails det = new TvDBDetails(xref.TvDBID);
                TvDetails[xref.TvDBID] = det;
            }
        }
    }
}
