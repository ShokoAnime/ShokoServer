using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using JMMServer;
using NLog;
using System.IO;
using JMMServer.AniDB_API.Raws;

namespace AniDBAPI
{
	public class AniDBHTTPHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static string AnimeURL
		{
			get
			{
				return "http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid={0}"; // they have said now that this will never change
			}
		}

		public static string MyListURL
		{
			get
			{
				return "http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={0}&pass={1}"; // they have said now that this will never change
			}
		}

		public static string VotesURL
		{
			get
			{
				return "http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=votes&user={0}&pass={1}"; // they have said now that this will never change
			}
		}

		public static void GetAnime(int animeID, bool createSeriesRecord)
		{
			try
			{
				string uri = string.Format(AniDBHTTPHelper.AnimeURL, animeID);
				//BaseConfig.MyAnimeLog.Write("GetAnime: {0}", uri);
				string xml = APIUtils.DownloadWebPage(uri);

				//BaseConfig.MyAnimeLog.Write("AniDBHTTPHelper.GetAnime: {0}", xml);

				if (xml.Trim().Length == 0) return;

				XmlDocument docAnime = new XmlDocument();
				docAnime.LoadXml(xml);

				ProcessAnimeDetails(docAnime, animeID);
				ProcessEpisodes(docAnime, animeID);
				
			}
			catch (Exception ex)
			{
				//BaseConfig.MyAnimeLog.Write("Error in AniDBHTTPHelper.GetAnime: {0}", ex);
				return;
			}
		}

		public static XmlDocument GetAnimeXMLFromAPI(int animeID, ref string rawXML)
		{
			try
			{
				string uri = string.Format(AniDBHTTPHelper.AnimeURL, animeID);
				//APIUtils.WriteToLog("GetAnimeXMLFromAPI: " + uri);
                rawXML = APIUtils.DownloadWebPage(uri);

				//APIUtils.WriteToLog("GetAnimeXMLFromAPI result: " + rawXML);

				if (rawXML.Trim().Length == 0) return null;

				XmlDocument docAnime = new XmlDocument();
				docAnime.LoadXml(rawXML);

				return docAnime;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.GetAnimeXMLFromAPI: {0}", ex);
				return null;
			}
		}

		public static XmlDocument GetMyListXMLFromAPI(string username, string password, ref string rawXML)
		{
			try
			{

				//string fileName = @"C:\Projects\SVN\mylist.xml";
				//StreamReader re = File.OpenText(fileName);
				//rawXML = re.ReadToEnd();
				//re.Close();


				string uri = string.Format(AniDBHTTPHelper.MyListURL, username, password);
				rawXML = APIUtils.DownloadWebPage(uri);

				if (rawXML.Trim().Length == 0) return null;

				XmlDocument docAnime = new XmlDocument();
				docAnime.LoadXml(rawXML);

				return docAnime;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.GetMyListXMLFromAPI: {0}", ex);
				return null;
			}
		}

		public static XmlDocument GetVotesXMLFromAPI(string username, string password, ref string rawXML)
		{
			try
			{
				string uri = string.Format(AniDBHTTPHelper.VotesURL, username, password);
				rawXML = APIUtils.DownloadWebPage(uri);

				if (rawXML.Trim().Length == 0) return null;

				XmlDocument docAnime = new XmlDocument();
				docAnime.LoadXml(rawXML);

				return docAnime;
			}
			catch (Exception ex)
			{
				//BaseConfig.MyAnimeLog.Write("Error in AniDBHTTPHelper.GetAnimeXMLFromAPI: {0}", ex);
				return null;
			}
		}

		public static Raw_AniDB_Anime ProcessAnimeDetails(XmlDocument docAnime, int animeID)
		{
			// most of the genral anime data will be over written by the UDP command
			Raw_AniDB_Anime anime = new Raw_AniDB_Anime();
			anime.AnimeID = animeID;

			// check if there is any data
			try
			{
				string id = docAnime["anime"].Attributes["id"].Value;
			}
			catch (Exception ex)
			{
				//BaseConfig.MyAnimeLog.Write("Invalid xml document: {0}", animeID.ToString());
				return null;
			}

			anime.Description = TryGetProperty(docAnime, "anime", "description");
			anime.AnimeTypeRAW = TryGetProperty(docAnime, "anime", "type");
			

			string episodecount = TryGetProperty(docAnime, "anime", "episodecount");
			int epCount = 0;
			int.TryParse(episodecount, out epCount);
			anime.EpisodeCount = epCount;
			anime.EpisodeCountNormal = epCount;

			int convertedAirDate = Utils.GetAniDBDateAsSeconds(TryGetProperty(docAnime, "anime", "startdate"));
			int convertedEndDate = Utils.GetAniDBDateAsSeconds(TryGetProperty(docAnime, "anime", "enddate"));

			//anime.AirDate = TryGetProperty(docAnime, "anime", "startdate");
			//anime.EndDate = TryGetProperty(docAnime, "anime", "enddate");

			anime.AirDate = Utils.GetAniDBDateAsDate(convertedAirDate);
			anime.EndDate = Utils.GetAniDBDateAsDate(convertedEndDate);

			anime.BeginYear = anime.AirDate.HasValue ? anime.AirDate.Value.Year : 0;
			anime.EndYear = anime.EndDate.HasValue ? anime.EndDate.Value.Year : 0;

			//string enddate = TryGetProperty(docAnime, "anime", "enddate");

			string restricted = docAnime["anime"].Attributes["restricted"].Value;
			bool res = false;
			bool.TryParse(restricted, out res);
			anime.Restricted = res ? 1 : 0;

			anime.URL = TryGetProperty(docAnime, "anime", "url");
			anime.Picname = TryGetProperty(docAnime, "anime", "picture");

			anime.DateTimeUpdated = DateTime.Now;
			anime.DateTimeDescUpdated = anime.DateTimeUpdated;
			anime.ImageEnabled = 1;
			
			#region Related Anime
			if (docAnime["anime"]["relatedanime"] != null)
			{
				XmlNodeList raItems = docAnime["anime"]["relatedanime"].GetElementsByTagName("anime");
				if (raItems != null)
				{
					anime.RelatedAnimeIdsRAW = "";
					anime.RelatedAnimeTypesRAW = "";

					foreach (XmlNode node in raItems)
					{
						try
						{
							int id = int.Parse(node.Attributes["id"].Value);
							int relType = ConvertReltTypeTextToEnum(TryGetAttribute(node, "type"));
                            
							if (anime.RelatedAnimeIdsRAW.Length > 0) anime.RelatedAnimeIdsRAW += "'";
							if (anime.RelatedAnimeTypesRAW.Length > 0) anime.RelatedAnimeTypesRAW += "'";

							anime.RelatedAnimeIdsRAW += id.ToString();
							anime.RelatedAnimeTypesRAW += relType.ToString();

						}
						catch (Exception ex)
						{
							//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
						}
					}
				}
			}
			#endregion

			#region Titles
			if (docAnime["anime"]["titles"] != null)
			{
				XmlNodeList titleItems = docAnime["anime"]["titles"].GetElementsByTagName("title");
				if (titleItems != null)
				{
					foreach (XmlNode node in titleItems)
					{
						try
						{
							string titleType = node.Attributes["type"].Value.Trim().ToLower();
							string languageType = node.Attributes["xml:lang"].Value.Trim().ToLower();
							string titleValue = node.InnerText.Trim();

							if (titleType.Trim().ToUpper() == "MAIN")
							{
								anime.MainTitle = titleValue;
							}
						}
						catch (Exception ex)
						{
							//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
						}
					}

				}
			}
			#endregion

			#region Ratings

			// init ratings
			anime.VoteCount = 0;
			anime.TempVoteCount = 0;
			anime.Rating = 0;
			anime.TempRating = 0;
			anime.ReviewCount = 0;
			anime.AvgReviewRating = 0;

			if (docAnime["anime"]["ratings"] != null)
			{
				XmlNodeList ratingItems = docAnime["anime"]["ratings"].ChildNodes;
				if (ratingItems != null)
				{
					foreach (XmlNode node in ratingItems)
					{
						try
						{
							if (node.Name.Trim().ToLower() == "permanent")
							{
								int iCount = 0;
								int.TryParse(TryGetAttribute(node, "count"), out iCount);
								anime.VoteCount = iCount;

								decimal iRating = 0;
								decimal.TryParse(node.InnerText.Trim(), out iRating);
								anime.Rating = (int)(iRating * 100);
							}
							if (node.Name.Trim().ToLower() == "temporary")
							{
								int iCount = 0;
								int.TryParse(TryGetAttribute(node, "count"), out iCount);
								anime.TempVoteCount = iCount;

								decimal iRating = 0;
								decimal.TryParse(node.InnerText.Trim(), out iRating);
								anime.TempRating = (int)(iRating * 100);
							}
							if (node.Name.Trim().ToLower() == "review")
							{
								int iCount = 0;
								int.TryParse(TryGetAttribute(node, "count"), out iCount);
								anime.ReviewCount = iCount;

								decimal iRating = 0;
								decimal.TryParse(node.InnerText.Trim(), out iRating);
								anime.AvgReviewRating = (int)(iRating * 100);
							}

						}
						catch (Exception ex)
						{
							//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
						}
					}
				}
			}
			#endregion

		    //anime.VersionNumber = Raw_AniDB_Anime.LastVersion;
			//BaseConfig.MyAnimeLog.Write("Anime: {0}", anime.ToString());
			return anime;
			//anime.Save(true, createSeriesRecord);

			//AniDB_Anime.UpdateDescription(anime.AnimeID, anime.Description);
		}

		public static List<Raw_AniDB_Episode> GetEpisodes(int animeID)
		{
			string xmlResult = "";
			XmlDocument docAnime = GetAnimeXMLFromAPI(animeID, ref xmlResult);
			if (docAnime == null)
				return null;
			return ProcessEpisodes(docAnime, animeID);
		}

		public static List<Raw_AniDB_Category> ProcessCategories(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_Category> categories = new List<Raw_AniDB_Category>();

			try
			{
				if (docAnime["anime"]["categories"] != null)
				{
					XmlNodeList categoryItems = docAnime["anime"]["categories"].GetElementsByTagName("category");
					if (categoryItems != null)
					{
						foreach (XmlNode node in categoryItems)
						{
							try
							{
								Raw_AniDB_Category category = new Raw_AniDB_Category();
								category.ProcessFromHTTPResult(node, animeID);
								categories.Add(category);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessCategories: {0}", ex);
				return null;
			}

			return categories;
		}

		public static List<Raw_AniDB_Tag> ProcessTags(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_Tag> tags = new List<Raw_AniDB_Tag>();

			try
			{
				if (docAnime["anime"]["tags"] != null)
				{
					XmlNodeList tagItems = docAnime["anime"]["tags"].GetElementsByTagName("tag");
					if (tagItems != null)
					{
						foreach (XmlNode node in tagItems)
						{
							try
							{
								Raw_AniDB_Tag tag = new Raw_AniDB_Tag();
								tag.ProcessFromHTTPResult(node, animeID);
								tags.Add(tag);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessTags: {0}", ex);
				return null;
			}

			return tags;
		}

		public static List<Raw_AniDB_Character> ProcessCharacters(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_Character> chars = new List<Raw_AniDB_Character>();

			try
			{
				if (docAnime["anime"]["characters"] != null)
				{
					XmlNodeList charItems = docAnime["anime"]["characters"].GetElementsByTagName("character");
					if (charItems != null)
					{
						foreach (XmlNode node in charItems)
						{
							try
							{
								Raw_AniDB_Character chr = new Raw_AniDB_Character();
								chr.ProcessFromHTTPResult(node, animeID);
								chars.Add(chr);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessCharacters: {0}", ex);
				return null;
			}

			return chars;
		}

		public static List<Raw_AniDB_Anime_Title> ProcessTitles(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_Anime_Title> titles = new List<Raw_AniDB_Anime_Title>();

			try
			{
				if (docAnime["anime"]["titles"] != null)
				{
					XmlNodeList titleItems = docAnime["anime"]["titles"].GetElementsByTagName("title");
					if (titleItems != null)
					{
						foreach (XmlNode node in titleItems)
						{
							try
							{
								Raw_AniDB_Anime_Title animeTitle = new Raw_AniDB_Anime_Title();
								animeTitle.ProcessFromHTTPResult(node, animeID);
								titles.Add(animeTitle);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				string msg = string.Format("Error in AniDBHTTPHelper.ProcessTitles: {0} - {1}", animeID, ex.ToString());
				logger.ErrorException(msg, ex);
				return null;
			}

			return titles;
		}

		public static List<Raw_AniDB_RelatedAnime> ProcessRelations(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_RelatedAnime> rels = new List<Raw_AniDB_RelatedAnime>();

			try
			{
				if (docAnime["anime"]["relatedanime"] != null)
				{
					XmlNodeList relItems = docAnime["anime"]["relatedanime"].GetElementsByTagName("anime");
					if (relItems != null)
					{
						foreach (XmlNode node in relItems)
						{
							try
							{
								Raw_AniDB_RelatedAnime rel = new Raw_AniDB_RelatedAnime();
								rel.ProcessFromHTTPResult(node, animeID);
								rels.Add(rel);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessRelations: {0}", ex);
				return null;
			}

			return rels;
		}

		public static List<Raw_AniDB_SimilarAnime> ProcessSimilarAnime(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_SimilarAnime> rels = new List<Raw_AniDB_SimilarAnime>();

			try
			{
				if (docAnime["anime"]["similaranime"] != null)
				{
					XmlNodeList simItems = docAnime["anime"]["similaranime"].GetElementsByTagName("anime");
					if (simItems != null)
					{
						foreach (XmlNode node in simItems)
						{
							try
							{
								Raw_AniDB_SimilarAnime sim = new Raw_AniDB_SimilarAnime();
								sim.ProcessFromHTTPResult(node, animeID);
								rels.Add(sim);
							}
							catch (Exception ex)
							{
								//BaseConfig.MyAnimeLog.Write("Error in GetEpisodes: {0}", ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessSimilarAnime: {0}", ex);
				return null;
			}

			return rels;
		}

		public static List<Raw_AniDB_Episode> ProcessEpisodes(XmlDocument docAnime, int animeID)
		{
			List<Raw_AniDB_Episode> eps = new List<Raw_AniDB_Episode>();

			try
			{
				if (docAnime != null && docAnime["anime"] != null && docAnime["anime"]["episodes"] != null)
				{
					XmlNodeList episodeItems = docAnime["anime"]["episodes"].GetElementsByTagName("episode");

					foreach (XmlNode node in episodeItems)
					{
						try
						{
							Raw_AniDB_Episode ep = new Raw_AniDB_Episode();
							ep.ProcessEpisodeSource(node, animeID);
							eps.Add(ep);
						}
						catch (Exception ex)
						{
							//BaseConfig.MyAnimeLog.Write("Error in ProcessEpisodes: {0}", ex);
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessEpisodes: {0}", ex);
				return null;
			}

			return eps;
		}

		public static List<Raw_AniDB_MyListFile> ProcessMyList(XmlDocument docAnime)
		{
			List<Raw_AniDB_MyListFile> mylistentries = new List<Raw_AniDB_MyListFile>();

			try
			{
				if (docAnime != null && docAnime["mylist"] != null)
				{
					XmlNodeList myitems = docAnime["mylist"].GetElementsByTagName("mylistitem");

					foreach (XmlNode node in myitems)
					{
						try
						{
							Raw_AniDB_MyListFile mylistitem = new Raw_AniDB_MyListFile();
							mylistitem.ProcessHTTPSource(node);
							mylistentries.Add(mylistitem);
						}
						catch (Exception ex)
						{

							logger.ErrorException("Error in ProcessEpisodes: {0}" + ex.ToString(), ex);
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in AniDBHTTPHelper.ProcessMyList: {0}", ex);
				return null;
			}

			return mylistentries;
		}

		public static List<Raw_AniDB_Vote_HTTP> ProcessVotes(XmlDocument docAnime)
		{
			List<Raw_AniDB_Vote_HTTP> myvotes = new List<Raw_AniDB_Vote_HTTP>();

			if (docAnime != null && docAnime["votes"] != null)
			{
				// get the permanent anime votes
				XmlNodeList myitems = null;
				try
				{
					myitems = docAnime["votes"]["anime"].GetElementsByTagName("vote");
					foreach (XmlNode node in myitems)
					{
						Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
						thisVote.ProcessAnime(node);
						myvotes.Add(thisVote);
					}
				}
				catch { }

				// get the temporary anime votes
				try
				{
					myitems = docAnime["votes"]["animetemporary"].GetElementsByTagName("vote");
					foreach (XmlNode node in myitems)
					{
						Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
						thisVote.ProcessAnimeTemp(node);
						myvotes.Add(thisVote);
					}
				}
				catch { }

				// get the episode votes
				try
				{
					myitems = docAnime["votes"]["episode"].GetElementsByTagName("vote");
					foreach (XmlNode node in myitems)
					{
					
							Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
							thisVote.ProcessEpisode(node);
							myvotes.Add(thisVote);
					
					}
				}
				catch { }
			}

			return myvotes;
		}

		public static int ConvertReltTypeTextToEnum(string relType)
		{
			if (relType.Trim().ToLower() == "sequel") return 1;
			if (relType.Trim().ToLower() == "prequel") return 2;
			if (relType.Trim().ToLower() == "same setting") return 11;
			if (relType.Trim().ToLower() == "alternative setting") return 21;
			if (relType.Trim().ToLower() == "alternative version") return 32;
			if (relType.Trim().ToLower() == "music video") return 41;
			if (relType.Trim().ToLower() == "character") return 42;
			if (relType.Trim().ToLower() == "side story") return 51;
			if (relType.Trim().ToLower() == "parent story") return 52;
			if (relType.Trim().ToLower() == "summary") return 61;
			if (relType.Trim().ToLower() == "full story") return 62;

			return 100;
		}

		public static string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
		{
			try
			{
				string prop = doc[keyName][propertyName].InnerText.Trim();
				return prop;
			}
			catch { }

			return "";
		}

		public static string TryGetProperty(XmlNode node, string propertyName)
		{
			try
			{
				string prop = node[propertyName].InnerText.Trim();
				return prop;
			}
			catch { }

			return "";
		}


		public static string TryGetPropertyWithAttribute(XmlNode node, string propertyName, string attName, string attValue)
		{
			try
			{
				string prop = "";
				foreach (XmlNode nodeChild in node.ChildNodes)
				{
					if (nodeChild.Name == propertyName)
					{
						if (nodeChild.Attributes[attName].Value == attValue)
						{
							prop = nodeChild.InnerText.Trim();
						}
					}
				}

				return prop;
			}
			catch { }

			return "";
		}

		public static string TryGetAttribute(XmlNode parentnode, string nodeName, string attName)
		{
			try
			{
				string prop = parentnode[nodeName].Attributes[attName].Value;
				return prop;
			}
			catch { }

			return "";
		}

		public static string TryGetAttribute(XmlNode node, string attName)
		{
			try
			{
				string prop = node.Attributes[attName].Value;
				return prop;
			}
			catch { }

			return "";
		}

		
	}
}
