using System;
using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Providers.TraktTV
{
    public class TraktSummaryContainer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // All the Trakt cross refs for this anime
        private List<CrossRef_AniDB_TraktV2> crossRefTraktV2;

        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, Trakt_Episode> dictTraktEpisodes;

        // Trakt ID
        public Dictionary<string, TraktDetailsContainer> TraktDetails = new Dictionary<string, TraktDetailsContainer>();

        public int AnimeID { get; set; }

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

        private void PopulateCrossRefs()
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                crossRefTraktV2 = repCrossRef.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void PopulateDictTraktEpisodes()
        {
            try
            {
                dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
                foreach (var det in TraktDetails.Values)
                {
                    if (det != null)
                    {
                        // create a dictionary of absolute episode numbers for Trakt episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        var eps = det.TraktEpisodes;

                        if (eps != null)
                        {
                            var i = 1;
                            foreach (var ep in eps)
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

            foreach (var xref in CrossRefTraktV2)
            {
                var det = new TraktDetailsContainer(xref.TraktID);
                TraktDetails[xref.TraktID] = det;
            }
        }
    }
}