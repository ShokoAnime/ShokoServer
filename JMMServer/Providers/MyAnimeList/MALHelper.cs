using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using NLog;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Commands.WebCache;
using JMMServer.Commands;
using JMMServer.Commands.MAL;
using System.Collections;
using AniDBAPI;
using System.Diagnostics;
using JMMContracts;

namespace JMMServer.Providers.MyAnimeList
{
	public class MALHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static anime SearchAnimesByTitle(string searchTitle)
		{
			if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
			{
				logger.Warn("Won't search MAL, MAL credentials not provided");
				return GetEmptyAnimes();
			}

			searchTitle = HttpUtility.UrlPathEncode(searchTitle);

			string searchResultXML = "";
			try
			{
				searchResultXML = SendMALAuthenticatedRequest("http://myanimelist.net/api/anime/search.xml?q=" + searchTitle);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return GetEmptyAnimes();
			}
			if (searchResultXML.Trim().Length == 0) return GetEmptyAnimes();

			searchResultXML = ReplaceEntityNamesByCharacter(searchResultXML);

			XmlSerializer serializer = new XmlSerializer(typeof(anime));
			XmlDocument docSearchResult = new XmlDocument();
			docSearchResult.LoadXml(searchResultXML);

			XmlNodeReader reader = new XmlNodeReader(docSearchResult.DocumentElement);
			object obj = serializer.Deserialize(reader);
			anime animes = (anime)obj;

			if (animes == null || animes.entry == null)
				animes = GetEmptyAnimes();

			return animes;
		}

		private static anime GetEmptyAnimes()
		{
			anime animes = new anime();
			animeEntry[] animeEntries = new animeEntry[0];
			animes.entry = animeEntries;
			return animes;
		}

		private static string SendMALAuthenticatedRequest(string url)
		{
			HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
			webReq.Timeout = 30 * 1000;
			webReq.Credentials = new NetworkCredential(ServerSettings.MAL_Username, ServerSettings.MAL_Password);
			webReq.PreAuthenticate = true;

			HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

			Stream responseStream = WebResponse.GetResponseStream();
			String enco = WebResponse.CharacterSet;
			Encoding encoding = null;
			if (!String.IsNullOrEmpty(enco))
				encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
			if (encoding == null)
				encoding = Encoding.Default;
			StreamReader Reader = new StreamReader(responseStream, encoding);

			string output = Reader.ReadToEnd();

			WebResponse.Close();
			responseStream.Close();

			return output;

		}

		public static bool VerifyCredentials()
		{

			try
			{
				string username = ServerSettings.MAL_Username;
				string password = ServerSettings.MAL_Password;

				string url = "http://myanimelist.net/api/account/verify_credentials.xml";
				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
				webReq.Timeout = 30 * 1000;
				webReq.Credentials = new NetworkCredential(username, password);
				webReq.PreAuthenticate = true;

				HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

				Stream responseStream = WebResponse.GetResponseStream();
				String enco = WebResponse.CharacterSet;
				Encoding encoding = null;
				if (!String.IsNullOrEmpty(enco))
					encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
				if (encoding == null)
					encoding = Encoding.Default;
				StreamReader Reader = new StreamReader(responseStream, encoding);

				string outputXML = Reader.ReadToEnd();

				WebResponse.Close();
				responseStream.Close();

				if (outputXML.Trim().Length == 0) return false;

				outputXML = ReplaceEntityNamesByCharacter(outputXML);

				XmlSerializer serializer = new XmlSerializer(typeof(user));
				XmlDocument docVerifyCredentials = new XmlDocument();
				docVerifyCredentials.LoadXml(outputXML);

				XmlNodeReader reader = new XmlNodeReader(docVerifyCredentials.DocumentElement);
				object obj = serializer.Deserialize(reader);
				user _user = (user)obj;

				if (_user.username.ToUpper() == username.ToUpper())
					return true;

				return false;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}

		public static string ReplaceEntityNamesByCharacter(string strInput)
		{
			//entity characters
			string[] entityCharacters ={
			        "'",
			        " ", "¡",	"¢",	"£",	"¤",	"¥",	"¦",	"§",
			        "¨",	"©",	"ª",	"«",	"¬",	"­",	"®",	"¯",	"°",	"±",	"²",	"³",	"´",
			        "µ",	"¶",	"·",	"¸",	"¹",	"º",	"»",	"¼",	"½",	"¾",	"¿",	"×",	"÷",
			        "À",	"Á",	"Â",	"Ã",	"Ä",	"Å",	"Æ",	"Ç",	"È",	"É",	"Ê",	"Ë",	"Ì",
			        "Í",	"Î",	"Ï",	"Ð",	"Ñ",	"Ò",	"Ó",	"Ô",	"Õ",	"Ö",	"Ø",	"Ù",	"Ú",
			        "Û",	"Ü",	"Ý",	"Þ",	"ß",	"à",	"á",	"â",	"ã",	"ä",	"å",	"æ",	"ç",
			        "è",	"é",	"ê",	"ë",	"ì",	"í",	"î",	"ï",	"ð",	"ñ",	"ò",	"ó",	"ô",
			        "õ",	"ö",	"ø",	"ù",	"ú",	"û",	"ü",	"ý",	"þ",	"ÿ"};
			//entity names to be replaced by entity characters
			string[] entityNames ={
			        "&apos;",
			        "&nbsp;", "&iexcl;",
			        "&cent;",	"&pound;",	"&curren;",	"&yen;",	"&brvbar;",	"&sect;",	"&uml;",
			        "&copy;",	"&ordf;",	"&laquo;",	"&not;",	"&shy;",	"&reg;",	"&macr;",
			        "&deg;",	"&plusmn;",	"&sup2;",	"&sup3;",	"&acute;",	"&micro;",	"&para;",	
			        "&middot;",	"&cedil;",	"&sup1;",	"&ordm;",	"&raquo;",	"&frac14;",	"&frac12;",
			        "&frac34;",	"&iquest;",	"&times;",	"&divide;",	"&Agrave;",	"&Aacute;",	"&Acirc;",	
			        "&Atilde;",	"&Auml;",	"&Aring;",	"&AElig;",	"&Ccedil;",	"&Egrave;",	"&Eacute;",
			        "&Ecirc;",	"&Euml;",	"&Igrave;",	"&Iacute;",	"&Icirc;",	"&Iuml;",	"&ETH;",	
			        "&Ntilde;",	"&Ograve;",	"&Oacute;",	"&Ocirc;",	"&Otilde;",	"&Ouml;",	"&Oslash;",
			        "&Ugrave;",	"&Uacute;",	"&Ucirc;",	"&Uuml;",	"&Yacute;",	"&THORN;",	"&szlig;",	
			        "&agrave;",	"&aacute;",	"&acirc;",	"&atilde;",	"&auml;",	"&aring;",	"&aelig;",	
			        "&ccedil;",	"&egrave;",	"&eacute;",	"&ecirc;",	"&euml;",	"&igrave;",	"&iacute;",
			        "&icirc;",	"&iuml;",	"&eth;",	"&ntilde;",	"&ograve;",	"&oacute;",	"&ocirc;",
			        "&otilde;",	"&ouml;",	"&oslash;",	"&ugrave;",	"&uacute;",	"&ucirc;",	"&uuml;",	
			        "&yacute;",	"&thorn;",	"&yuml;"};


			if (entityCharacters.Length != entityNames.Length)
				return strInput;

			for (int i = 0; i < entityNames.Length; i++)
			{
				strInput = strInput.Replace(entityNames[i], entityCharacters[i]);
			}

			return strInput;

		}

		public static void LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber, bool fromWebCache)
		{
			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			CrossRef_AniDB_MAL xrefTemp = repCrossRef.GetByMALID(malID);
			if (xrefTemp != null)
			{
				string msg = string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1}", malID, xrefTemp.AnimeID);
				logger.Warn(msg);
				return;
			}

			CrossRef_AniDB_MAL xref = new CrossRef_AniDB_MAL();
			xref.AnimeID = animeID;
			xref.MALID = malID;
			xref.MALTitle = malTitle;
			xref.StartEpisodeType = epType;
			xref.StartEpisodeNumber = epNumber;
			if (fromWebCache)
				xref.CrossRefSource = (int)CrossRefSource.WebCache;
			else
				xref.CrossRefSource = (int)CrossRefSource.User;

			repCrossRef.Save(xref);

			StatsCache.Instance.UpdateUsingAnime(animeID);

			logger.Trace("Changed MAL association: {0}", animeID);

			CommandRequest_MALUpdatedWatchedStatus cmd = new CommandRequest_MALUpdatedWatchedStatus(animeID);
			cmd.Save();

			CommandRequest_WebCacheSendXRefAniDBMAL req = new CommandRequest_WebCacheSendXRefAniDBMAL(xref.CrossRef_AniDB_MALID);
			req.Save();
		}

		public static void RemoveLinkAniDBMAL(int animeID, int epType, int epNumber)
		{
			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			CrossRef_AniDB_MAL xref = repCrossRef.GetByAnimeConstraint(animeID, epType, epNumber);
			if (xref == null) return;

			repCrossRef.Delete(xref.CrossRef_AniDB_MALID);

			CommandRequest_WebCacheDeleteXRefAniDBMAL req = new CommandRequest_WebCacheDeleteXRefAniDBMAL(animeID, epType, epNumber);
			req.Save();
		}

		public static void ScanForMatches()
		{
			if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
			{
				logger.Warn("Won't SCAN MAL, MAL credentials not provided");
				return;
			}

			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> allSeries = repSeries.GetAll();

			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			foreach (AnimeSeries ser in allSeries)
			{
				AniDB_Anime anime = ser.Anime;
				if (anime == null) continue;

				// don't scan if it is associated on the TvDB
				if (anime.CrossRefMAL == null || anime.CrossRefMAL.Count == 0)
				{
					logger.Trace(string.Format("Found anime without MAL association: {0} ({1})", anime.AnimeID, anime.MainTitle));

					CommandRequest_MALSearchAnime cmd = new CommandRequest_MALSearchAnime(ser.AniDB_ID, false);
					cmd.Save();
				}
			}

		}

		// non official API to retrieve current watching state
		public static myanimelist GetMALAnimeList()
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
				{
					logger.Warn("Won't search MAL, MAL credentials not provided");
					return null;
				}

				string url = string.Format("http://myanimelist.net/malappinfo.php?u={0}&status=all&type=anime", ServerSettings.MAL_Username);
				string malAnimeListXML = SendMALAuthenticatedRequest(url);
				malAnimeListXML = ReplaceEntityNamesByCharacter(malAnimeListXML);

				XmlSerializer serializer = new XmlSerializer(typeof(myanimelist));
				XmlDocument docAnimeList = new XmlDocument();
				docAnimeList.LoadXml(malAnimeListXML);

				XmlNodeReader reader = new XmlNodeReader(docAnimeList.DocumentElement);
				object obj = serializer.Deserialize(reader);
				myanimelist malAnimeList = (myanimelist)obj;
				return malAnimeList;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		/*public static void UpdateWatchedStatus(int animeID, enEpisodeType epType, int lastWatchedEpNumber)
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
					return;

				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				List<AniDB_Episode> aniEps = repAniEps.GetByAnimeIDAndEpisodeTypeNumber(animeID, epType, lastWatchedEpNumber);
				if (aniEps.Count == 0) return;

				AnimeEpisodeRepository repEp = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEp.GetByAniDBEpisodeID(aniEps[0].EpisodeID);
				if (ep == null) return;

				MALHelper.UpdateMAL(ep);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}*/

		public static void UpdateMALSeries(AnimeSeries ser)
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
					return;

				// Populate MAL animelist hashtable if isNeverDecreaseWatched set
				Hashtable animeListHashtable = new Hashtable();
				if (ServerSettings.MAL_NeverDecreaseWatchedNums) //if set, check watched number before update: take some time, as user anime list must be loaded
				{
					myanimelist malAnimeList = GetMALAnimeList();
					if (malAnimeList != null && malAnimeList.anime != null)
					{
						for (int i = 0; i < malAnimeList.anime.Length; i++)
						{
							animeListHashtable.Add(malAnimeList.anime[i].series_animedb_id, malAnimeList.anime[i]);
						}
					}
				}

				// look for MAL Links
				List<CrossRef_AniDB_MAL> crossRefs = ser.Anime.CrossRefMAL;
				if (crossRefs == null || crossRefs.Count == 0)
				{
					logger.Warn("Could not find MAL link for : {0} ({1})", ser.Anime.FormattedTitle, ser.Anime.AnimeID);
					return;
				}

				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AniDB_FileRepository repFiles = new AniDB_FileRepository();

				List<AnimeEpisode> eps = ser.AnimeEpisodes;

				// find the anidb user
				JMMUserRepository repUsers = new JMMUserRepository();
				List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();
				if (aniDBUsers.Count == 0) return;

				JMMUser user = aniDBUsers[0];

				int score = 0;
				if (ser.Anime.UserVote != null)
					score = (int)(ser.Anime.UserVote.VoteValue / 100);

				// e.g.
				// AniDB - Code Geass R2
				// MAL Equivalent = AniDB Normal Eps 1 - 25 / Code Geass: Hangyaku no Lelouch R2 / hxxp://myanimelist.net/anime/2904/Code_Geass:_Hangyaku_no_Lelouch_R2
				// MAL Equivalent = AniDB Special Eps 1 - 9 / Code Geass: Hangyaku no Lelouch R2 Picture Drama / hxxp://myanimelist.net/anime/5163/Code_Geass:_Hangyaku_no_Lelouch_R2_Picture_Drama
				// MAL Equivalent = AniDB Special Eps 9 - 18 / Code Geass: Hangyaku no Lelouch R2: Flash Specials / hxxp://myanimelist.net/anime/9591/Code_Geass:_Hangyaku_no_Lelouch_R2:_Flash_Specials
				// MAL Equivalent = AniDB Special Eps 20 / Code Geass: Hangyaku no Lelouch - Kiseki no Birthday Picture Drama / hxxp://myanimelist.net/anime/8728/Code_Geass:_Hangyaku_no_Lelouch_-_Kiseki_no_Birthday_Picture_Drama

				foreach (CrossRef_AniDB_MAL xref in crossRefs)
				{
					// look for the right MAL id
					int malID = -1;
					int epNumber = -1;
					int totalEpCount = -1;

					List<string> fanSubGroups = new List<string>();

					// for each cross ref (which is a series on MAL) we need to update the data
					// so find all the episodes which apply to this cross ref
					int lastWatchedEpNumber = 0;
					int downloadedEps = 0;

					foreach (AnimeEpisode ep in eps)
					{
						int epNum = ep.AniDB_Episode.EpisodeNumber;
						if (xref.StartEpisodeType == (int)ep.EpisodeTypeEnum && epNum >= xref.StartEpisodeNumber &&
							epNum <= GetUpperEpisodeLimit(crossRefs, xref))
						{
							malID = xref.MALID;
							epNumber = epNum - xref.StartEpisodeNumber + 1;

							// find the total episode count
							if (totalEpCount < 0)
							{
								if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode) totalEpCount = ser.Anime.EpisodeCountNormal;
								if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special) totalEpCount = ser.Anime.EpisodeCountSpecial;
								totalEpCount = totalEpCount - xref.StartEpisodeNumber + 1;
							}

							// any episodes here belong to the MAL series
							// find the latest watched episod enumber
							AnimeEpisode_User usrRecord = ep.GetUserRecord(user.JMMUserID);
							if (usrRecord != null && usrRecord.WatchedDate.HasValue && epNum > lastWatchedEpNumber)
							{
								lastWatchedEpNumber = epNum;
							}

							List<Contract_VideoDetailed> contracts = ep.GetVideoDetailedContracts(user.JMMUserID);

							// find the latest episode number in the collection
							if (contracts.Count > 0)
								downloadedEps++;

							foreach (Contract_VideoDetailed contract in contracts)
							{
								if (!string.IsNullOrEmpty(contract.AniDB_Anime_GroupNameShort) && !fanSubGroups.Contains(contract.AniDB_Anime_GroupNameShort))
									fanSubGroups.Add(contract.AniDB_Anime_GroupNameShort);
							}
						}
					}

					string fanSubs = "";
					foreach (string fgrp in fanSubGroups)
					{
						if (!string.IsNullOrEmpty(fanSubs)) fanSubs += ",";
						fanSubs += fgrp;
					}

					// determine status
					int status = 1; //watching
					if (animeListHashtable.ContainsKey(malID))
					{
						myanimelistAnime animeInList = (myanimelistAnime)animeListHashtable[malID];
						status = animeInList.my_status;
					}

					// over-ride is user has watched an episode
					// don't override on hold (3) or dropped (4) but do override plan to watch (6)
					if (status == 6 && lastWatchedEpNumber > 0) status = 1; //watching
					if (lastWatchedEpNumber == totalEpCount) status = 2; //completed

					if (lastWatchedEpNumber > totalEpCount)
					{
						logger.Error("updateMAL, episode number > matching anime episode total : {0} ({1}) / {2}", ser.Anime.FormattedTitle, ser.Anime.AnimeID, epNumber);
						continue;
					}

					if (malID <= 0 || totalEpCount <= 0)
					{
						logger.Warn("Could not find MAL link for : {0} ({1})", ser.Anime.FormattedTitle, ser.Anime.AnimeID);
						continue;
					}
					else
					{
						bool res = UpdateAnime(malID, lastWatchedEpNumber, status, score, downloadedEps, fanSubs);

						string confirmationMessage = string.Format("MAL successfully updated, mal id: {0}, ep: {1}, score: {2}", malID, lastWatchedEpNumber, score);
						if (res) logger.Trace(confirmationMessage);
					}
					
				}

				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}



		private static int GetUpperEpisodeLimit(List<CrossRef_AniDB_MAL> crossRefs, CrossRef_AniDB_MAL xrefBase)
		{
			foreach (CrossRef_AniDB_MAL xref in crossRefs)
			{
				if (xref.StartEpisodeType == xrefBase.StartEpisodeType)
				{
					if (xref.StartEpisodeNumber > xrefBase.StartEpisodeNumber)
						return xref.StartEpisodeNumber - 1;
				}
			}

			return int.MaxValue;
		}


		public static bool AddAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				string res = "";

				string animeValuesXMLString = string.Format("?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score><downloaded_episodes>{3}</downloaded_episodes><fansub_group>{4}</fansub_group></entry>",
							lastEpisodeWatched, status, score, downloadedEps, fanSubs);

				res = SendMALAuthenticatedRequest("http://myanimelist.net/api/animelist/add/" + animeId + ".xml" + animeValuesXMLString);

				return true;
			}
			catch (WebException)
			{
				return false;
			}
		}

		public static bool ModifyAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				string res = "";

				string animeValuesXMLString = string.Format("?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score><downloaded_episodes>{3}</downloaded_episodes><fansub_group>{4}</fansub_group></entry>",
							lastEpisodeWatched, status, score, downloadedEps, fanSubs);

				res = SendMALAuthenticatedRequest("http://myanimelist.net/api/animelist/update/" + animeId + ".xml" + animeValuesXMLString);

				return true;
			}
			catch (WebException)
			{
				return false;
			}
		}

		// status: 1/watching, 2/completed, 3/onhold, 4/dropped, 6/plantowatch
		public static bool UpdateAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				

				string res = "";
				try
				{
					// now modify back to proper status
					if (!AddAnime(animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs))
					{
						ModifyAnime(animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs);
					}

					
				}
				catch (WebException)
				{
			
					// if nothing good happens
					logger.Error("MAL update anime failed: " + res);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}
		}
	}
}
