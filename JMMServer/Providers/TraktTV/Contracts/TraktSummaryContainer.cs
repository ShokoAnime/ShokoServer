using System;
using System.Collections.Generic;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using NLog;

namespace JMMServer.Providers.TraktTV
{
    public class TraktSummaryContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int AnimeID { get; set; }

        // Trakt ID
        public Dictionary<string, TraktDetailsContainer> TraktDetails = new Dictionary<string, TraktDetailsContainer>();

        // All the Trakt cross refs for this anime
        private List<SVR_CrossRef_AniDB_TraktV2> crossRefTraktV2 = null;

        public List<SVR_CrossRef_AniDB_TraktV2> CrossRefTraktV2
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
                crossRefTraktV2 = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
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
                logger.Error( ex,ex.ToString());
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
                logger.Error( ex,ex.ToString());
            }
        }

        private void PopulateTraktDetails()
        {
            if (CrossRefTraktV2 == null) return;

            foreach (SVR_CrossRef_AniDB_TraktV2 xref in CrossRefTraktV2)
            {
                TraktDetailsContainer det = new TraktDetailsContainer(xref.TraktID);
                TraktDetails[xref.TraktID] = det;
            }
        }
    }
}