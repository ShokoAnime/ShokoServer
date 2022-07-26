using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Server;

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

        private List<Raw_AniDB_Tag> tags = new List<Raw_AniDB_Tag>();

        public List<Raw_AniDB_Tag> Tags
        {
            get => tags;
            set => tags = value;
        }

        private List<Raw_AniDB_Staff> staff = new List<Raw_AniDB_Staff>();

        public List<Raw_AniDB_Staff> Staff
        {
            get => staff;
            set => staff = value;
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
            return "AniDBHTTPCommand_GetFullAnime_" + AnimeID;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingAnimeHTTP;
        }

        public virtual AniDBUDPResponseCode Process()
        {
            XmlDocument docAnime = null;

            logger.Info("Trying to load " + AnimeID + " anime data from cache.");
            logger.Trace($"Getting anime info: Force: {ForceFromAniDB}  CacheOnly: {CacheOnly}");
            var xmlUtils = ShokoServer.ServiceContainer.GetRequiredService<HttpXmlUtils>();
            if (CacheOnly || !ForceFromAniDB)
            {
                logger.Trace("Trying to load Anime HTTP info from cache file...");
                var rawXml = xmlUtils.LoadAnimeHTTPFromFile(animeID);
                if (!string.IsNullOrEmpty(rawXml))
                {
                    docAnime = new XmlDocument();
                    docAnime.LoadXml(rawXml);
                }


                if (docAnime == null && !CacheOnly)
                {
                    logger.Trace("No Anime HTTP info found in cache file for " + animeID +", loading from HTTP API");
                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID);
                }
            }
            else if (!CacheOnly)
            {
                logger.Trace("Forced update. Trying to load " + AnimeID + " anime data from AniDB API.");
                docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID);

                if (docAnime == null)
                {
                    logger.Trace("Can't download " + AnimeID + ". Banned or not found. Trying to load from cache anyway.");
                    var rawXml = xmlUtils.LoadAnimeHTTPFromFile(animeID);
                    if (!string.IsNullOrEmpty(rawXml))
                    {
                        docAnime = new XmlDocument();
                        docAnime.LoadXml(rawXml);
                    }
                }
            }

            if (docAnime != null)
            {
                logger.Trace("Anime data loaded for " + AnimeID + ". Processing and saving it.");
                anime = AniDBHTTPHelper.ProcessAnimeDetails(docAnime, animeID);
                if (anime == null) return AniDBUDPResponseCode.NoSuchAnime;

                episodes = AniDBHTTPHelper.ProcessEpisodes(docAnime, animeID);
                titles = AniDBHTTPHelper.ProcessTitles(docAnime, animeID);
                tags = AniDBHTTPHelper.ProcessTags(docAnime, animeID);
                staff = AniDBHTTPHelper.ProcessStaff(docAnime, animeID);
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
                return AniDBUDPResponseCode.GotAnimeInfoHTTP;
            }
            return AniDBUDPResponseCode.NoSuchAnime;
        }


        public AniDBHTTPCommand_GetFullAnime()
        {
            commandType = enAniDBCommandType.GetAnimeInfoHTTP;
        }

        public void Init(int animeID, bool createSeriesRecord, bool forceFromAniDB, bool cacheOnly)
        {
            ForceFromAniDB = forceFromAniDB;
            CacheOnly = cacheOnly;
            this.animeID = animeID;
            commandID = animeID.ToString();
            createAnimeSeriesRecord = createSeriesRecord;
        }
    }
}
