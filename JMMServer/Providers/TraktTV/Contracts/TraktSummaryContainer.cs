using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMModels.Childs;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;

namespace JMMServer.Providers.TraktTV
{
    public class TraktSummaryContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public JMMModels.AnimeSerie Serie { get; set; }

        // Trakt ID
        public Dictionary<string, TraktDetailsContainer> TraktDetails = new Dictionary<string, TraktDetailsContainer>();

        public List<AniDB_Anime_Trakt> CrossRefTraktV2 => Serie?.AniDB_Anime.Trakts ?? new List<AniDB_Anime_Trakt>();
        /*
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
        */
        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, Episode_TraktEpisode> dictTraktEpisodes = null;
        public Dictionary<int, Episode_TraktEpisode> DictTraktEpisodes
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
                dictTraktEpisodes = new Dictionary<int, Episode_TraktEpisode>();
                foreach (TraktDetailsContainer det in TraktDetails.Values)
                {
                    if (det != null)
                    {

                        // create a dictionary of absolute episode numbers for Trakt episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        List<Episode_TraktEpisode> eps = det.TraktEpisodes;

                        int i = 1;
                        foreach (Episode_TraktEpisode ep in eps)
                        {
                            // ignore episode 0, this can't be mapped to Trakt
                            if (ep.Number > 0)
                            {
                                dictTraktEpisodes[i] = ep;
                                i++;
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

     

        public void Populate(JMMModels.AnimeSerie serie)
        {
            Serie = serie;
            
            try
            {
                //PopulateCrossRefs();
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
            //if (CrossRefTraktV2 == null) return;

            foreach (AniDB_Anime_Trakt xref in Serie.AniDB_Anime.Trakts)
            {
                TraktDetailsContainer det = new TraktDetailsContainer(Serie,xref);
                TraktDetails[xref.TraktId] = det;
            }
        }
    }
}
