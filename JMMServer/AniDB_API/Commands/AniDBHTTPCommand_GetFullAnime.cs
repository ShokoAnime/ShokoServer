using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using JMMServer;
using JMMServer.AniDB_API.Raws;
using JMMServer.Providers.Azure;

namespace AniDBAPI.Commands
{
	public class AniDBHTTPCommand_GetFullAnime : AniDBHTTPCommand, IAniDBHTTPCommand
	{
		private int animeID;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		public bool ForceFromAniDB {get; set;}
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
			string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			string filePath = Path.Combine(appPath, "Anime_HTTP");

			if (!Directory.Exists(filePath))
				Directory.CreateDirectory(filePath);

			string fileName = string.Format("AnimeDoc_{0}.xml", animeID);
			string fileNameWithPath = Path.Combine(filePath, fileName);

			XmlDocument docAnime = null;
			if (File.Exists(fileNameWithPath))
			{
				StreamReader re = File.OpenText(fileNameWithPath);
				string rawXML = re.ReadToEnd();
				re.Close();

				docAnime = new XmlDocument();
				docAnime.LoadXml(rawXML);
			}

			return docAnime;
		}

		private void WriteAnimeHTTPToFile(int animeID, string xml)
		{
			string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			string filePath = Path.Combine(appPath, "Anime_HTTP");

			if (!Directory.Exists(filePath))
				Directory.CreateDirectory(filePath);

			string fileName = string.Format("AnimeDoc_{0}.xml", animeID);
			string fileNameWithPath = Path.Combine(filePath, fileName);

			StreamWriter sw;
			sw = File.CreateText(fileNameWithPath);
			sw.Write(xml);
			sw.Close();
		}

		public virtual enHelperActivityType Process()
		{
			string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			string filePath = Path.Combine(appPath, "Anime_HTTP");

			if (!Directory.Exists(filePath))
				Directory.CreateDirectory(filePath);

			string fileName = string.Format("AnimeDoc_{0}.xml", animeID);
			string fileNameWithPath = Path.Combine(filePath, fileName);

			if (!CacheOnly)
			{
				JMMService.LastAniDBMessage = DateTime.Now;
				JMMService.LastAniDBHTTPMessage = DateTime.Now;
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

                    docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID, ref xmlResult);
				}
				else
				{
					docAnime = AniDBHTTPHelper.GetAnimeXMLFromAPI(animeID, ref xmlResult);
					//XmlDocument docAnime = LoadAnimeHTTPFromFile(animeID);
				}
			}

			if (xmlResult.Trim().Length > 0)
				WriteAnimeHTTPToFile(animeID, xmlResult);

			if (CheckForBan(xmlResult)) return enHelperActivityType.NoSuchAnime;

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
