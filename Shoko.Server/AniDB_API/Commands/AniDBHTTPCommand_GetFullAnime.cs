using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Shoko.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Models.Azure;
using NLog;

namespace AniDBAPI.Commands
{
    public class AniDBHTTPCommand_GetFullAnime : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private int animeID;

        public int AnimeID
        {
            get => animeID;
            set => animeID = value;
        }

        public bool ForceFromAniDB { get; set; }
        public bool CacheOnly { get; set; }


        private Raw_AniDB_Anime anime;

        public Raw_AniDB_Anime Anime
        {
            get => anime;
            set => anime = value;
        }

        private List<Raw_AniDB_Episode> episodes = new List<Raw_AniDB_Episode>();

        public List<Raw_AniDB_Episode> Episodes
        {
            get => episodes;
            set => episodes = value;
        }

        private List<Raw_AniDB_Anime_Title> titles = new List<Raw_AniDB_Anime_Title>();

        public List<Raw_AniDB_Anime_Title> Titles
        {
            get => titles;
            set => titles = value;
        }

        private List<Raw_AniDB_Category> categories = new List<Raw_AniDB_Category>();

        public List<Raw_AniDB_Category> Categories
        {
            get => categories;
            set => categories = value;
        }

        private List<Raw_AniDB_Tag> tags = new List<Raw_AniDB_Tag>();

        public List<Raw_AniDB_Tag> Tags
        {
            get => tags;
            set => tags = value;
        }

        private List<Raw_AniDB_Character> characters = new List<Raw_AniDB_Character>();

        public List<Raw_AniDB_Character> Characters
        {
            get => characters;
            set => characters = value;
        }

        private List<Raw_AniDB_ResourceLink> resources = new List<Raw_AniDB_ResourceLink>();

        public List<Raw_AniDB_ResourceLink> Resources
        {
            get => resources;
            set => resources = value;
        }

        private List<Raw_AniDB_RelatedAnime> relations = new List<Raw_AniDB_RelatedAnime>();

        public List<Raw_AniDB_RelatedAnime> Relations
        {
            get => relations;
            set => relations = value;
        }

        private List<Raw_AniDB_SimilarAnime> similarAnime = new List<Raw_AniDB_SimilarAnime>();

        public List<Raw_AniDB_SimilarAnime> SimilarAnime
        {
            get => similarAnime;
            set => similarAnime = value;
        }

        private List<Raw_AniDB_Recommendation> recommendations = new List<Raw_AniDB_Recommendation>();

        public List<Raw_AniDB_Recommendation> Recommendations
        {
            get => recommendations;
            set => recommendations = value;
        }

        private bool createAnimeSeriesRecord = true;

        public bool CreateAnimeSeriesRecord
        {
            get => createAnimeSeriesRecord;
            set => createAnimeSeriesRecord = value;
        }

        public string GetKey()
        {
            return "AniDBHTTPCommand_GetFullAnime_" + AnimeID.ToString();
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingAnimeHTTP;
        }

        public virtual enHelperActivityType Process()
        {
            XmlDocument docAnime = null;

            if (CacheOnly || !ForceFromAniDB)
            {
                //logger.Info("Trying to load Anime HTTP info from cache file...");
                logger.Info("Not forced update. This may be because it was updated recently. Trying to load " + AnimeID + " anime data from cache.");
                docAnime = APIUtils.LoadAnimeHTTPFromFile(animeID);
                

                if (docAnime == null && !CacheOnly)
                {
                    logger.Info("No Anime HTTP info found in cache file for " + animeID +", loading from HTTP API");
                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID);
                }
            }
            else if (!CacheOnly)
            {
                logger.Info("Forced update. Trying to load " + AnimeID + " anime data from AniDB API.");
                docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID);

                if (docAnime == null)
                {
                    logger.Info("Can't download " + AnimeID + ". Banned or not found. Trying to load from cache anyway.");
                    docAnime = APIUtils.LoadAnimeHTTPFromFile(animeID);
                }
            }

            if (docAnime != null)
            {
                logger.Info("Anime data loaded for " + AnimeID + ". Processing and saving it.");
                anime = AniDBHTTPHelper.ProcessAnimeDetails(docAnime, animeID);
                if (anime == null) return enHelperActivityType.NoSuchAnime;

                episodes = AniDBHTTPHelper.ProcessEpisodes(docAnime, animeID);
                titles = AniDBHTTPHelper.ProcessTitles(docAnime, animeID);
                tags = AniDBHTTPHelper.ProcessTags(docAnime, animeID);
                characters = AniDBHTTPHelper.ProcessCharacters(docAnime, animeID);
                resources = AniDBHTTPHelper.ProcessResources(docAnime, animeID);

                if (!CacheOnly)
                {
                    relations = AniDBHTTPHelper.ProcessRelations(docAnime, animeID);
                    similarAnime = AniDBHTTPHelper.ProcessSimilarAnime(docAnime, animeID);
                    recommendations = AniDBHTTPHelper.ProcessRecommendations(docAnime, animeID);
                }
                else
                {
                    relations = null;
                    similarAnime = null;
                    recommendations = null;
                }
                return enHelperActivityType.GotAnimeInfoHTTP;
            }
            return enHelperActivityType.NoSuchAnime;
        }


        public AniDBHTTPCommand_GetFullAnime()
        {
            commandType = enAniDBCommandType.GetAnimeInfoHTTP;
        }

        public void Init(int animeID, bool createSeriesRecord, bool forceFromAniDB, bool cacheOnly)
        {
            this.ForceFromAniDB = forceFromAniDB;
            this.CacheOnly = cacheOnly;
            this.animeID = animeID;
            commandID = animeID.ToString();
            this.createAnimeSeriesRecord = createSeriesRecord;
        }
    }
}