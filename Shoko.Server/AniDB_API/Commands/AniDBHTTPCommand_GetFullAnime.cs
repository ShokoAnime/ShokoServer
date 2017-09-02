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
            get { return animeID; }
            set { animeID = value; }
        }

        public bool ForceFromAniDB { get; set; }
        public bool CacheOnly { get; set; }


        private Raw_AniDB_Anime anime;

        public Raw_AniDB_Anime Anime
        {
            get { return anime; }
            set { anime = value; }
        }

        private List<Raw_AniDB_Episode> episodes = new List<Raw_AniDB_Episode>();

        public List<Raw_AniDB_Episode> Episodes
        {
            get { return episodes; }
            set { episodes = value; }
        }

        private List<Raw_AniDB_Anime_Title> titles = new List<Raw_AniDB_Anime_Title>();

        public List<Raw_AniDB_Anime_Title> Titles
        {
            get { return titles; }
            set { titles = value; }
        }

        private List<Raw_AniDB_Category> categories = new List<Raw_AniDB_Category>();

        public List<Raw_AniDB_Category> Categories
        {
            get { return categories; }
            set { categories = value; }
        }

        private List<Raw_AniDB_Tag> tags = new List<Raw_AniDB_Tag>();

        public List<Raw_AniDB_Tag> Tags
        {
            get { return tags; }
            set { tags = value; }
        }

        private List<Raw_AniDB_Character> characters = new List<Raw_AniDB_Character>();

        public List<Raw_AniDB_Character> Characters
        {
            get { return characters; }
            set { characters = value; }
        }

        private List<Raw_AniDB_RelatedAnime> relations = new List<Raw_AniDB_RelatedAnime>();

        public List<Raw_AniDB_RelatedAnime> Relations
        {
            get { return relations; }
            set { relations = value; }
        }

        private List<Raw_AniDB_SimilarAnime> similarAnime = new List<Raw_AniDB_SimilarAnime>();

        public List<Raw_AniDB_SimilarAnime> SimilarAnime
        {
            get { return similarAnime; }
            set { similarAnime = value; }
        }

        private List<Raw_AniDB_Recommendation> recommendations = new List<Raw_AniDB_Recommendation>();

        public List<Raw_AniDB_Recommendation> Recommendations
        {
            get { return recommendations; }
            set { recommendations = value; }
        }

        private bool createAnimeSeriesRecord = true;

        public bool CreateAnimeSeriesRecord
        {
            get { return createAnimeSeriesRecord; }
            set { createAnimeSeriesRecord = value; }
        }

        private string xmlResult = "";

        public string XmlResult
        {
            get { return xmlResult; }
            set { xmlResult = value; }
        }

        public string GetKey()
        {
            return "AniDBHTTPCommand_GetFullAnime_" + AnimeID.ToString();
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingAnimeHTTP;
        }

        private XmlDocument LoadAnimeHTTPFromFile(int animeID)
        {
            string filePath = ServerSettings.AnimeXmlDirectory;


            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            string fileName = $"AnimeDoc_{animeID}.xml";
            string fileNameWithPath = Path.Combine(filePath, fileName);

            if (!File.Exists(fileNameWithPath)) return null;
            using (StreamReader re = File.OpenText(fileNameWithPath))
            {
                string rawXML = re.ReadToEnd();

                var docAnime = new XmlDocument();
                docAnime.LoadXml(rawXML);
                return docAnime;
            }
        }

        private void WriteAnimeHTTPToFile(int animeID, string xml)
        {
            try
            {
                string filePath = ServerSettings.AnimeXmlDirectory;

                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);

                string fileName = $"AnimeDoc_{animeID}.xml";
                string fileNameWithPath = Path.Combine(filePath, fileName);

                // First check to make sure we not rights issue
                if (!Utils.IsDirectoryWritable(filePath))
                    Utils.GrantAccess(filePath);

                // Check again and only if write-able we create it
                if (Utils.IsDirectoryWritable(filePath))
                {
                    using (var sw = File.CreateText(fileNameWithPath))
                    {
                        sw.Write(xml);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error occurred during WriteAnimeHTTPToFile(): {ex}");
            }
        }

        public virtual enHelperActivityType Process()
        {
            if (!CacheOnly)
            {
                ShokoService.LastAniDBMessage = DateTime.Now;
                ShokoService.LastAniDBHTTPMessage = DateTime.Now;
            }

            XmlDocument docAnime = null;

            if (CacheOnly)
            {
                xmlResult = AzureWebAPI.Get_AnimeXML(animeID);
                if (!string.IsNullOrEmpty(xmlResult))
                {
                    docAnime = new XmlDocument();
                    docAnime.LoadXml(xmlResult);
                }
            }
            else
            {
                if (!ForceFromAniDB)
                {
                    //Disable usage of Azure API for this type of data
                    /*xmlResult = AzureWebAPI.Get_AnimeXML(animeID);
					if (string.IsNullOrEmpty(xmlResult))
					{
						docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID, ref xmlResult);
					}
					else
					{
						docAnime = new XmlDocument();
						docAnime.LoadXml(xmlResult);
					}*/

                    //logger.Info("Trying to load Anime HTTP info from cache file...");
                    docAnime = LoadAnimeHTTPFromFile(animeID);
                    if (docAnime == null)
                    {
                        //logger.Info("No Anime HTTP info found in cache file, loading from HTTP API");
                        docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID, ref xmlResult);
                    }
                }
                else
                {
                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID, ref xmlResult);
                }
            }

            if (CheckForBan(xmlResult)) return enHelperActivityType.Banned_555;

            if (xmlResult.Trim().Length > 0)
                WriteAnimeHTTPToFile(animeID, xmlResult);
            
            if (docAnime != null)
            {
                anime = AniDBHTTPHelper.ProcessAnimeDetails(docAnime, animeID);
                episodes = AniDBHTTPHelper.ProcessEpisodes(docAnime, animeID);
                titles = AniDBHTTPHelper.ProcessTitles(docAnime, animeID);
                tags = AniDBHTTPHelper.ProcessTags(docAnime, animeID);
                characters = AniDBHTTPHelper.ProcessCharacters(docAnime, animeID);
                relations = AniDBHTTPHelper.ProcessRelations(docAnime, animeID);
                similarAnime = AniDBHTTPHelper.ProcessSimilarAnime(docAnime, animeID);
                recommendations = AniDBHTTPHelper.ProcessRecommendations(docAnime, animeID);
                return enHelperActivityType.GotAnimeInfoHTTP;
            }
            else
            {
                return enHelperActivityType.NoSuchAnime;
            }
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