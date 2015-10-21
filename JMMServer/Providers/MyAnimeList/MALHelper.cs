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
using System.Globalization;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMModels.Extensions;
using JMMServer.Extensions;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;
using JMMUser = JMMServer.Entities.JMMUser;

namespace JMMServer.Providers.MyAnimeList
{
	public class MALHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static anime SearchAnimesByTitle(string userid, string searchTitle)
		{
		    JMMModels.JMMUser user=Store.JmmUserRepo.Find(userid);
            if (user == null)
                return GetEmptyAnimes(); 
		    user = user.GetUserWithAuth(AuthorizationProvider.MAL);
            if (user == null)
                return GetEmptyAnimes();
		    UserNameAuthorization auth = user.GetMALAuthorization();

			if (string.IsNullOrEmpty(auth.UserName) || string.IsNullOrEmpty(auth.Password))
			{
				logger.Warn("Won't search MAL, MAL credentials not provided");
				return GetEmptyAnimes();
			}

			searchTitle = HttpUtility.UrlPathEncode(searchTitle);

			string searchResultXML = "";
			try
			{
				searchResultXML = SendMALAuthenticatedRequest(user.Id, "http://myanimelist.net/api/anime/search.xml?q=" + searchTitle);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return GetEmptyAnimes();
			}
			if (searchResultXML.Trim().Length == 0) return GetEmptyAnimes();

			searchResultXML = HttpUtility.HtmlDecode(searchResultXML);
			searchResultXML = HttpUtilityV2.HtmlDecode(searchResultXML);
			searchResultXML = searchResultXML.Replace("&", "&amp;");
			XmlDocument docSearchResult = new XmlDocument();
			docSearchResult.LoadXml(searchResultXML);

			anime animes = GetEmptyAnimes();
			List<animeEntry> entries = new List<animeEntry>();

			if (docSearchResult != null && docSearchResult["anime"] != null)
			{
				XmlNodeList searchItems = docSearchResult["anime"].GetElementsByTagName("entry");

				foreach (XmlNode node in searchItems)
				{
					try
					{
						animeEntry entry = new animeEntry();
						// default values
						entry.end_date = string.Empty;
						entry.english = string.Empty;
						entry.episodes = 0;
						entry.id = 0;
						entry.image = string.Empty;
						entry.score = 0;
						entry.start_date = string.Empty;
						entry.status = string.Empty;
						entry.synonyms = string.Empty;
						entry.synopsis = string.Empty;
						entry.title = string.Empty;
						entry.type = string.Empty;

						entry.end_date = AniDBHTTPHelper.TryGetProperty(node, "end_date");
						entry.english = AniDBHTTPHelper.TryGetProperty(node, "english");
						entry.image = AniDBHTTPHelper.TryGetProperty(node, "image");

						int eps = 0;
						int id = 0;
						decimal score = 0;

						int.TryParse(AniDBHTTPHelper.TryGetProperty(node, "episodes"), out eps);
						int.TryParse(AniDBHTTPHelper.TryGetProperty(node, "id"), out id);
						decimal.TryParse(AniDBHTTPHelper.TryGetProperty(node, "score"), out score);

						entry.episodes = eps;
						entry.id = id;
						entry.score = score;

						entry.start_date = AniDBHTTPHelper.TryGetProperty(node, "start_date");
						entry.status = AniDBHTTPHelper.TryGetProperty(node, "status");
						entry.synonyms = AniDBHTTPHelper.TryGetProperty(node, "synonyms");
						entry.synopsis = AniDBHTTPHelper.TryGetProperty(node, "synopsis");
						entry.title = AniDBHTTPHelper.TryGetProperty(node, "title");
						entry.type = AniDBHTTPHelper.TryGetProperty(node, "type");

						entries.Add(entry);
					}
					catch (Exception ex)
					{
						logger.ErrorException(ex.ToString(), ex);
					}
				}
			}

			animes.entry = entries.ToArray();
			return animes;
		}

		private static anime GetEmptyAnimes()
		{
			anime animes = new anime();
			animeEntry[] animeEntries = new animeEntry[0];
			animes.entry = animeEntries;
			return animes;
		}

		private static string SendMALAuthenticatedRequest(string userid, string url)
		{
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
		    if (user == null)
		        return string.Empty;
            user = user.GetUserWithAuth(AuthorizationProvider.MAL);
		    if (user == null)
		        return string.Empty;
            UserNameAuthorization auth = user.GetMALAuthorization();

            HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
			webReq.Timeout = 30 * 1000;
			webReq.Credentials = new NetworkCredential(auth.UserName, auth.Password);
			webReq.PreAuthenticate = true;
            webReq.UserAgent = "api-jmm-7EC61C5283B99DC1CFE9A3730BF507CE";

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

		public static bool VerifyCredentials(string userid)
		{

			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
			    if (user == null)
			        return false;
                user = user.GetUserWithAuth(AuthorizationProvider.MAL);
			    if (user == null)
			        return false;
                UserNameAuthorization auth = user.GetMALAuthorization();


			    string username = auth.UserName;
			    string password = auth.Password;

				string url = "http://myanimelist.net/api/account/verify_credentials.xml";
				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
				webReq.Timeout = 30 * 1000;
				webReq.Credentials = new NetworkCredential(username, password);
				webReq.PreAuthenticate = true;
                webReq.UserAgent = "api-jmm-7EC61C5283B99DC1CFE9A3730BF507CE";

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

		public static void LinkAniDBMAL(string userid, int animeID, int malID, string malTitle, int epType, int epNumber, bool fromWebCache)
		{
		    List<AnimeSerie> ser=Store.AnimeSerieRepo.AnimeSeriesFromMAL(malID.ToString());
            if (ser!=null && ser.Count>0)
            { 
				string msg = $"Not using MAL link as this MAL ID ({malID}) is already in use by {ser[0].AniDB_Anime.Id}";
				logger.Warn(msg);
				return;
			}
		    AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(animeID.ToString());
            serie.AniDB_Anime.MALs=new List<AniDB_Anime_MAL>();
            AniDB_Anime_MAL xref=new AniDB_Anime_MAL();

			xref.MalId = malID.ToString();
			xref.Title = malTitle;
			xref.StartEpisodeType = (AniDB_Episode_Type)epType;
			xref.StartEpisodeNumber = epNumber;
			xref.CrossRefSource = fromWebCache ? CrossRefSourceType.WebCache : CrossRefSourceType.User;
            Store.AnimeSerieRepo.Save(serie, UpdateType.None);
			logger.Trace("Changed MAL association: {0}", animeID);

			CommandRequest_MALUpdatedWatchedStatus cmd = new CommandRequest_MALUpdatedWatchedStatus(userid, animeID);
			cmd.Save();

		    CommandRequest_WebCacheSendXRefAniDBMAL req = new CommandRequest_WebCacheSendXRefAniDBMAL(animeID, malID);
			req.Save();
		}

		public static void RemoveLinkAniDBMAL(int animeID, int epType, int epNumber)
		{
		    AnimeSerie ser = Store.AnimeSerieRepo.Find(animeID.ToString());
		    if (ser == null)
		        return;
            JMMModels.Childs.AniDB_Anime_MAL mal=ser.AniDB_Anime.MALs.FirstOrDefault(a=>a.StartEpisodeType==(AniDB_Episode_Type)epType && a.StartEpisodeNumber==epNumber);
		    if (mal == null)
		        return;
		    ser.AniDB_Anime.MALs.Remove(mal);
            Store.AnimeSerieRepo.Save(ser,UpdateType.None);

			CommandRequest_WebCacheDeleteXRefAniDBMAL req = new CommandRequest_WebCacheDeleteXRefAniDBMAL(animeID, epType, epNumber);
			req.Save();
		}

		public static void ScanForMatches(string userid)
		{
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
		    if (user == null)
		        return;
            user = user.GetUserWithAuth(AuthorizationProvider.MAL);
		    if (user == null)
		        return;
		    UserNameAuthorization auth = user.GetMALAuthorization();

            if (string.IsNullOrEmpty(auth.UserName) || string.IsNullOrEmpty(auth.Password))
			{
				logger.Warn("Won't SCAN MAL, MAL credentials not provided");
				return;
			}
			foreach (AnimeSerie ser in Store.AnimeSerieRepo.AsQueryable().Where(a=>!a.AniDB_Anime.IsMALLinkDisabled() && a.AniDB_Anime.MALs==null || a.AniDB_Anime.MALs.Count==0))
			{
				logger.Trace($"Found anime without MAL association: {ser.AniDB_Anime.Id} ({ser.AniDB_Anime.MainTitle})");
				CommandRequest_MALSearchAnime cmd = new CommandRequest_MALSearchAnime(int.Parse(ser.AniDB_Anime.Id), false);
				cmd.Save();
			}

		}

		// non official API to retrieve current watching state
		public static myanimelist GetMALAnimeList(string userid)
		{
			try
			{

			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
			    if (user == null)
			        return null;
                user = user.GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (user == null)
			        return null;
                UserNameAuthorization auth = user.GetMALAuthorization();

                if (string.IsNullOrEmpty(auth.UserName) || string.IsNullOrEmpty(auth.Password))
				{
					logger.Warn("Won't search MAL, MAL credentials not provided");
					return null;
				}

				string url = string.Format("http://myanimelist.net/malappinfo.php?u={0}&status=all&type=anime", ServerSettings.MAL_Username);
				string malAnimeListXML = SendMALAuthenticatedRequest(user.Id, url);
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

		public static void UpdateMALSeries(string userid, AnimeSerie ser)
		{
			try
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
			    if (user == null)
			        return;
                JMMModels.JMMUser userauth = user.GetUserWithAuth(AuthorizationProvider.MAL);
			    JMMModels.JMMUser useranidb = user.GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (userauth == null) 
			        return;
			    UserNameAuthorization auth = userauth.GetMALAuthorization();
			    if (auth == null)
			        return;

				// Populate MAL animelist hashtable if isNeverDecreaseWatched set
				Hashtable animeListHashtable = new Hashtable();
				if (ServerSettings.MAL_NeverDecreaseWatchedNums) //if set, check watched number before update: take some time, as user anime list must be loaded
				{
					myanimelist malAnimeList = GetMALAnimeList(userauth.Id);
					if (malAnimeList != null && malAnimeList.anime != null)
					{
						for (int i = 0; i < malAnimeList.anime.Length; i++)
						{
							animeListHashtable.Add(malAnimeList.anime[i].series_animedb_id, malAnimeList.anime[i]);
						}
					}
				}

				// look for MAL Links
                
				if (ser.AniDB_Anime.MALs == null || ser.AniDB_Anime.MALs.Count == 0)
				{
					logger.Warn("Could not find MAL link for : {0} ({1})", ser.AniDB_Anime.GetFormattedTitle(), ser.AniDB_Anime.Id);
					return;
				}

				/*AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AniDB_FileRepository repFiles = new AniDB_FileRepository();

				List<AnimeEpisode> eps = ser.GetAnimeEpisodes();

				// find the anidb user
				JMMUserRepository repUsers = new JMMUserRepository();
				List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();
				if (aniDBUsers.Count == 0) return;

				JMMUser user = aniDBUsers[0];
                */
				float score = 0;
			    if (useranidb != null)
			    {
			        JMMModels.Childs.AniDB_Vote vote = ser.AniDB_Anime.MyVotes.FirstOrDefault(a => a.JMMUserId == useranidb.Id);
			        if (vote != null)
			            score = vote.Vote;
			    }
			    // e.g.
				// AniDB - Code Geass R2
				// MAL Equivalent = AniDB Normal Eps 1 - 25 / Code Geass: Hangyaku no Lelouch R2 / hxxp://myanimelist.net/anime/2904/Code_Geass:_Hangyaku_no_Lelouch_R2
				// MAL Equivalent = AniDB Special Eps 1 - 9 / Code Geass: Hangyaku no Lelouch R2 Picture Drama / hxxp://myanimelist.net/anime/5163/Code_Geass:_Hangyaku_no_Lelouch_R2_Picture_Drama
				// MAL Equivalent = AniDB Special Eps 9 - 18 / Code Geass: Hangyaku no Lelouch R2: Flash Specials / hxxp://myanimelist.net/anime/9591/Code_Geass:_Hangyaku_no_Lelouch_R2:_Flash_Specials
				// MAL Equivalent = AniDB Special Eps 20 / Code Geass: Hangyaku no Lelouch - Kiseki no Birthday Picture Drama / hxxp://myanimelist.net/anime/8728/Code_Geass:_Hangyaku_no_Lelouch_-_Kiseki_no_Birthday_Picture_Drama

				foreach (JMMModels.Childs.AniDB_Anime_MAL xref in ser.AniDB_Anime.MALs)
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

                    foreach (JMMModels.Childs.AnimeEpisode ep in ser.Episodes.Where(a => a.AniDbEpisodes.Any(b => b.Value.Any(c => c.Type == xref.StartEpisodeType))))
                    {
                        int epNum = ep.GetAniDB_Episode().Number;
                        int maxep = ser.AniDB_Anime.MALs.Where(a => a.StartEpisodeType == xref.StartEpisodeType).Max(a => a.StartEpisodeNumber);
                        if (maxep > xref.StartEpisodeNumber)
                            maxep = maxep - 1;
                        else
                            maxep = int.MaxValue;
                        if (epNum >= xref.StartEpisodeNumber && epNum <= maxep)
                        {
                            malID = int.Parse(xref.MalId);
							epNumber = epNum - xref.StartEpisodeNumber + 1;

							// find the total episode count
							if (totalEpCount < 0)
							{
								if (xref.StartEpisodeType == AniDB_Episode_Type.RegularEpisode) totalEpCount = ser.AniDB_Anime.EpisodeCountNormal;
								if (xref.StartEpisodeType == AniDB_Episode_Type.Special) totalEpCount = ser.AniDB_Anime.EpisodeCountSpecial;
								totalEpCount = totalEpCount - xref.StartEpisodeNumber + 1;
							}

							// any episodes here belong to the MAL series
							// find the latest watched episod enumber

                            UserStats stats = ep.UsersStats.FirstOrDefault(a => a.JMMUserId == useranidb.Id);
	
							if (stats != null && stats.WatchedDate.HasValue && epNum > lastWatchedEpNumber)
							{
								lastWatchedEpNumber = epNum;
							}

							// find the latest episode number in the collection
							if (ep.AniDbEpisodes.SelectMany(a=>a.Value).SelectMany(a=>a.VideoLocals).Any())
								downloadedEps++;

						}
					}
                    List<string> fansubs = ser.Episodes.SelectMany(a=>a.AniDbEpisodes).SelectMany(a=>a.Value).Where(a=>a.Type== xref.StartEpisodeType).SelectMany(a=>a.Files).Select(a=>a.GroupNameShort).Distinct().ToList();
				    string fanSubs = string.Join(",", fansubs);



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
						logger.Error("updateMAL, episode number > matching anime episode total : {0} ({1}) / {2}", ser.AniDB_Anime.GetFormattedTitle(), ser.AniDB_Anime.Id, epNumber);
						continue;
					}

					if (malID <= 0 || totalEpCount <= 0)
					{
						logger.Warn("Could not find MAL link for : {0} ({1})", ser.AniDB_Anime.GetFormattedTitle(), ser.AniDB_Anime.Id);
						continue;
					}
					else
					{
						bool res = UpdateAnime(userauth.Id, malID, lastWatchedEpNumber, status, (int)score, downloadedEps, fanSubs);

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


        /*
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
        */

		public static bool AddAnime(string userid, int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				string res = "";

				string animeValuesXMLString = string.Format("?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score><downloaded_episodes>{3}</downloaded_episodes><fansub_group>{4}</fansub_group></entry>",
							lastEpisodeWatched, status, score, downloadedEps, fanSubs);

				res = SendMALAuthenticatedRequest(userid, "http://myanimelist.net/api/animelist/add/" + animeId + ".xml" + animeValuesXMLString);

				return true;
			}
			catch (WebException)
			{
				return false;
			}
		}

		public static bool ModifyAnime(string userid, int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				string res = "";

				string animeValuesXMLString = string.Format("?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score><downloaded_episodes>{3}</downloaded_episodes><fansub_group>{4}</fansub_group></entry>",
							lastEpisodeWatched, status, score, downloadedEps, fanSubs);

				res = SendMALAuthenticatedRequest(userid, "http://myanimelist.net/api/animelist/update/" + animeId + ".xml" + animeValuesXMLString);

				return true;
			}
			catch (WebException)
			{
				return false;
			}
		}

		// status: 1/watching, 2/completed, 3/onhold, 4/dropped, 6/plantowatch
		public static bool UpdateAnime(string userid, int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps, string fanSubs)
		{
			try
			{
				

				string res = "";
				try
				{
					// now modify back to proper status
					if (!AddAnime(userid,animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs))
					{
						ModifyAnime(userid, animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs);
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

	internal class HtmlEntities
	{
		// Fields
		private static string[] _entitiesList = new string[] { 
            "\"-quot", "&-amp", "<-lt", ">-gt", "\x00a0-nbsp", "\x00a1-iexcl", "\x00a2-cent", "\x00a3-pound", "\x00a4-curren", "\x00a5-yen", "\x00a6-brvbar", "\x00a7-sect", "\x00a8-uml", "\x00a9-copy", "\x00aa-ordf", "\x00ab-laquo", 
            "\x00ac-not", "\x00ad-shy", "\x00ae-reg", "\x00af-macr", "\x00b0-deg", "\x00b1-plusmn", "\x00b2-sup2", "\x00b3-sup3", "\x00b4-acute", "\x00b5-micro", "\x00b6-para", "\x00b7-middot", "\x00b8-cedil", "\x00b9-sup1", "\x00ba-ordm", "\x00bb-raquo", 
            "\x00bc-frac14", "\x00bd-frac12", "\x00be-frac34", "\x00bf-iquest", "\x00c0-Agrave", "\x00c1-Aacute", "\x00c2-Acirc", "\x00c3-Atilde", "\x00c4-Auml", "\x00c5-Aring", "\x00c6-AElig", "\x00c7-Ccedil", "\x00c8-Egrave", "\x00c9-Eacute", "\x00ca-Ecirc", "\x00cb-Euml", 
            "\x00cc-Igrave", "\x00cd-Iacute", "\x00ce-Icirc", "\x00cf-Iuml", "\x00d0-ETH", "\x00d1-Ntilde", "\x00d2-Ograve", "\x00d3-Oacute", "\x00d4-Ocirc", "\x00d5-Otilde", "\x00d6-Ouml", "\x00d7-times", "\x00d8-Oslash", "\x00d9-Ugrave", "\x00da-Uacute", "\x00db-Ucirc", 
            "\x00dc-Uuml", "\x00dd-Yacute", "\x00de-THORN", "\x00df-szlig", "\x00e0-agrave", "\x00e1-aacute", "\x00e2-acirc", "\x00e3-atilde", "\x00e4-auml", "\x00e5-aring", "\x00e6-aelig", "\x00e7-ccedil", "\x00e8-egrave", "\x00e9-eacute", "\x00ea-ecirc", "\x00eb-euml", 
            "\x00ec-igrave", "\x00ed-iacute", "\x00ee-icirc", "\x00ef-iuml", "\x00f0-eth", "\x00f1-ntilde", "\x00f2-ograve", "\x00f3-oacute", "\x00f4-ocirc", "\x00f5-otilde", "\x00f6-ouml", "\x00f7-divide", "\x00f8-oslash", "\x00f9-ugrave", "\x00fa-uacute", "\x00fb-ucirc", 
            "\x00fc-uuml", "\x00fd-yacute", "\x00fe-thorn", "\x00ff-yuml", "Œ-OElig", "œ-oelig", "Š-Scaron", "š-scaron", "Ÿ-Yuml", "ƒ-fnof", "ˆ-circ", "˜-tilde", "Α-Alpha", "Β-Beta", "Γ-Gamma", "Δ-Delta", 
            "Ε-Epsilon", "Ζ-Zeta", "Η-Eta", "Θ-Theta", "Ι-Iota", "Κ-Kappa", "Λ-Lambda", "Μ-Mu", "Ν-Nu", "Ξ-Xi", "Ο-Omicron", "Π-Pi", "Ρ-Rho", "Σ-Sigma", "Τ-Tau", "Υ-Upsilon", 
            "Φ-Phi", "Χ-Chi", "Ψ-Psi", "Ω-Omega", "α-alpha", "β-beta", "γ-gamma", "δ-delta", "ε-epsilon", "ζ-zeta", "η-eta", "θ-theta", "ι-iota", "κ-kappa", "λ-lambda", "μ-mu", 
            "ν-nu", "ξ-xi", "ο-omicron", "π-pi", "ρ-rho", "ς-sigmaf", "σ-sigma", "τ-tau", "υ-upsilon", "φ-phi", "χ-chi", "ψ-psi", "ω-omega", "ϑ-thetasym", "ϒ-upsih", "ϖ-piv", 
            " -ensp", " -emsp", " -thinsp", "‌-zwnj", "‍-zwj", "‎-lrm", "‏-rlm", "–-ndash", "—-mdash", "‘-lsquo", "’-rsquo", "‚-sbquo", "“-ldquo", "”-rdquo", "„-bdquo", "†-dagger", 
            "‡-Dagger", "•-bull", "…-hellip", "‰-permil", "′-prime", "″-Prime", "‹-lsaquo", "›-rsaquo", "‾-oline", "⁄-frasl", "€-euro", "ℑ-image", "℘-weierp", "ℜ-real", "™-trade", "ℵ-alefsym", 
            "←-larr", "↑-uarr", "→-rarr", "↓-darr", "↔-harr", "↵-crarr", "⇐-lArr", "⇑-uArr", "⇒-rArr", "⇓-dArr", "⇔-hArr", "∀-forall", "∂-part", "∃-exist", "∅-empty", "∇-nabla", 
            "∈-isin", "∉-notin", "∋-ni", "∏-prod", "∑-sum", "−-minus", "∗-lowast", "√-radic", "∝-prop", "∞-infin", "∠-ang", "∧-and", "∨-or", "∩-cap", "∪-cup", "∫-int", 
            "∴-there4", "∼-sim", "≅-cong", "≈-asymp", "≠-ne", "≡-equiv", "≤-le", "≥-ge", "⊂-sub", "⊃-sup", "⊄-nsub", "⊆-sube", "⊇-supe", "⊕-oplus", "⊗-otimes", "⊥-perp", 
        };
		private static Hashtable _entitiesLookupTable;
		private static object _lookupLockObject = new object();

		internal static char Lookup(string entity)
		{
			if (_entitiesLookupTable == null)
			{
				lock (_lookupLockObject)
				{
					if (_entitiesLookupTable == null)
					{
						Hashtable hashtable = new Hashtable();
						foreach (string str in _entitiesList)
						{
							hashtable[str.Substring(2)] = str[0];
						}
						_entitiesLookupTable = hashtable;
					}
				}
			}
			object obj2 = _entitiesLookupTable[entity];
			if (obj2 != null)
			{
				return (char)obj2;
			}
			return '\0';
		}
	}

	public sealed class HttpUtilityV2
	{
		private static char[] s_entityEndingChars = new char[] { ';', '&' };

		public static string HtmlDecode(string s)
		{
			if (s == null)
			{
				return null;
			}
			if (s.IndexOf('&') < 0)
			{
				return s;
			}
			StringBuilder sb = new StringBuilder();
			StringWriter output = new StringWriter(sb);
			HtmlDecode(s, output);
			return sb.ToString();
		}

		public static void HtmlDecode(string s, TextWriter output)
		{
			if (s != null)
			{
				if (s.IndexOf('&') < 0)
				{
					output.Write(s);
				}
				else
				{
					int length = s.Length;
					for (int i = 0; i < length; i++)
					{
						char ch = s[i];
						if (ch == '&')
						{
							int num3 = s.IndexOfAny(s_entityEndingChars, i + 1);
							if ((num3 > 0) && (s[num3] == ';'))
							{
								string entity = s.Substring(i + 1, (num3 - i) - 1);
								if ((entity.Length > 1) && (entity[0] == '#'))
								{
									try
									{
										if ((entity[1] == 'x') || (entity[1] == 'X'))
										{
											ch = (char)int.Parse(entity.Substring(2), NumberStyles.AllowHexSpecifier);
										}
										else
										{
											ch = (char)int.Parse(entity.Substring(1));
										}
										i = num3;
									}
									catch (FormatException)
									{
										i++;
									}
									catch (ArgumentException)
									{
										i++;
									}
								}
								else
								{
									i = num3;
									char ch2 = HtmlEntities.Lookup(entity);
									if (ch2 != '\0')
									{
										ch = ch2;
									}
									else
									{
										output.Write('&');
										output.Write(entity);
										output.Write(';');
										goto Label_0103;
									}
								}
							}
						}
						output.Write(ch);
					Label_0103: ;
					}
				}
			}
		}
	}
}
