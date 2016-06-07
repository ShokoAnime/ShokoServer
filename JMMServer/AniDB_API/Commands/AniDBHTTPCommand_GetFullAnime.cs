using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using JMMServer;
using JMMServer.Providers.Azure;

namespace AniDBAPI.Commands
{
    public class AniDBHTTPCommand_GetFullAnime : AniDBHTTPCommand, IAniDBHTTPCommand
    {
        private string xmlResult = "";


        public AniDBHTTPCommand_GetFullAnime()
        {
            commandType = enAniDBCommandType.GetAnimeInfoHTTP;
        }

        public int AnimeID { get; set; }

        public bool ForceFromAniDB { get; set; }
        public bool CacheOnly { get; set; }

        public Raw_AniDB_Anime Anime { get; set; }

        public List<Raw_AniDB_Episode> Episodes { get; set; } = new List<Raw_AniDB_Episode>();

        public List<Raw_AniDB_Anime_Title> Titles { get; set; } = new List<Raw_AniDB_Anime_Title>();

        public List<Raw_AniDB_Category> Categories { get; set; } = new List<Raw_AniDB_Category>();

        public List<Raw_AniDB_Tag> Tags { get; set; } = new List<Raw_AniDB_Tag>();

        public List<Raw_AniDB_Character> Characters { get; set; } = new List<Raw_AniDB_Character>();

        public List<Raw_AniDB_RelatedAnime> Relations { get; set; } = new List<Raw_AniDB_RelatedAnime>();

        public List<Raw_AniDB_SimilarAnime> SimilarAnime { get; set; } = new List<Raw_AniDB_SimilarAnime>();

        public List<Raw_AniDB_Recommendation> Recommendations { get; set; } = new List<Raw_AniDB_Recommendation>();

        public bool CreateAnimeSeriesRecord { get; set; } = true;

        public string XmlResult
        {
            get { return xmlResult; }
            set { xmlResult = value; }
        }

        public string GetKey()
        {
            return "AniDBHTTPCommand_GetFullAnime_" + AnimeID;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingAnimeHTTP;
        }

        public virtual enHelperActivityType Process()
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(appPath, "Anime_HTTP");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var fileName = string.Format("AnimeDoc_{0}.xml", AnimeID);
            var fileNameWithPath = Path.Combine(filePath, fileName);

            if (!CacheOnly)
            {
                JMMService.LastAniDBMessage = DateTime.Now;
                JMMService.LastAniDBHTTPMessage = DateTime.Now;
            }

            XmlDocument docAnime = null;

            if (CacheOnly)
            {
                xmlResult = AzureWebAPI.Get_AnimeXML(AnimeID);
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

                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(AnimeID, ref xmlResult);
                }
                else
                {
                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(AnimeID, ref xmlResult);
                    //XmlDocument docAnime = LoadAnimeHTTPFromFile(animeID);
                }
            }

            if (xmlResult.Trim().Length > 0)
                WriteAnimeHTTPToFile(AnimeID, xmlResult);

            if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

            if (docAnime != null)
            {
                Anime = AniDBHTTPHelper.ProcessAnimeDetails(docAnime, AnimeID);
                Episodes = AniDBHTTPHelper.ProcessEpisodes(docAnime, AnimeID);
                Titles = AniDBHTTPHelper.ProcessTitles(docAnime, AnimeID);
                Tags = AniDBHTTPHelper.ProcessTags(docAnime, AnimeID);
                Characters = AniDBHTTPHelper.ProcessCharacters(docAnime, AnimeID);
                Relations = AniDBHTTPHelper.ProcessRelations(docAnime, AnimeID);
                SimilarAnime = AniDBHTTPHelper.ProcessSimilarAnime(docAnime, AnimeID);
                Recommendations = AniDBHTTPHelper.ProcessRecommendations(docAnime, AnimeID);
                return enHelperActivityType.GotAnimeInfoHTTP;
            }
            return enHelperActivityType.NoSuchAnime;
        }

        private XmlDocument LoadAnimeHTTPFromFile(int animeID)
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(appPath, "Anime_HTTP");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var fileName = string.Format("AnimeDoc_{0}.xml", animeID);
            var fileNameWithPath = Path.Combine(filePath, fileName);

            XmlDocument docAnime = null;
            if (File.Exists(fileNameWithPath))
            {
                var re = File.OpenText(fileNameWithPath);
                var rawXML = re.ReadToEnd();
                re.Close();

                docAnime = new XmlDocument();
                docAnime.LoadXml(rawXML);
            }

            return docAnime;
        }

        private void WriteAnimeHTTPToFile(int animeID, string xml)
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(appPath, "Anime_HTTP");

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var fileName = string.Format("AnimeDoc_{0}.xml", animeID);
            var fileNameWithPath = Path.Combine(filePath, fileName);

            StreamWriter sw;
            sw = File.CreateText(fileNameWithPath);
            sw.Write(xml);
            sw.Close();
        }

        public void Init(int animeID, bool createSeriesRecord, bool forceFromAniDB, bool cacheOnly)
        {
            ForceFromAniDB = forceFromAniDB;
            CacheOnly = cacheOnly;
            AnimeID = animeID;
            commandID = animeID.ToString();
            CreateAnimeSeriesRecord = createSeriesRecord;
        }
    }
}