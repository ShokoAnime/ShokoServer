using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;

namespace JMMServer.Providers.TraktTV
{
    public class TraktSummaryContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int AnimeID { get; set; }

        // Trakt ID
        public Dictionary<string, TraktDetailsContainer> TraktDetails = new Dictionary<string, TraktDetailsContainer>();

        // All the Trakt cross refs for this anime
        private List<CrossRef_AniDB_TraktV2> crossRefTraktV2 = null;
        public List<CrossRef_AniDB_TraktV2> CrossRefTraktV2
        {
            get
            {
                if (crossRefTraktV2 == null)
                {
                    PopulateCrossRefs();
                }
                return crossRefTraktV2;
            }
        }

        private void PopulateCrossRefs()
        {
            try
            {
                CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                crossRefTraktV2 = repCrossRef.GetByAnimeID(AnimeID);

            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
        public Dictionary<int, Trakt_Episode> DictTraktEpisodes
        {
            get
            {
                if (dictTraktEpisodes == null)
                {
                    PopulateDictTraktEpisodes();
                }
                return dictTraktEpisodes;
            }
        }

        private void PopulateDictTraktEpisodes()
        {
            try
            {
                dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
                foreach (TraktDetailsContainer det in TraktDetails.Values)
                {
                    if (det != null)
                    {

                        // create a dictionary of absolute episode numbers for Trakt episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        List<Trakt_Episode> eps = det.TraktEpisodes;

                        if (eps != null)
                        {
                            int i = 1;
                            foreach (Trakt_Episode ep in eps)
                            {
                                // ignore episode 0, this can't be mapped to Trakt
                                if (ep.EpisodeNumber > 0)
                                {
                                    dictTraktEpisodes[i] = ep;
                                    i++;
                                }
                            }
                        }
                    }
                }
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
                PopulateTraktDetails();
                PopulateDictTraktEpisodes();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void PopulateTraktDetails()
        {
            if (CrossRefTraktV2 == null) return;

            foreach (CrossRef_AniDB_TraktV2 xref in CrossRefTraktV2)
            {
                TraktDetailsContainer det = new TraktDetailsContainer(xref.TraktID);
                TraktDetails[xref.TraktID] = det;
            }
        }
    }
}
