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

		public static void LinkAniDBMAL(int animeID, int malID, string malTitle, bool fromWebCache)
		{
			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			CrossRef_AniDB_MAL xrefTemp = repCrossRef.GetByMALID(malID);
			if (xrefTemp != null)
			{
				string msg = string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1}", malID, xrefTemp.AnimeID);
				logger.Warn(msg);
				return;
			}

			xrefTemp = repCrossRef.GetByAnimeID(animeID);
			if (xrefTemp != null)
			{
				string msg = string.Format("Not using MAL link as this Anime ID ({0}) is already in use by {1} ({2})", animeID, xrefTemp.MALID, xrefTemp.MALTitle);
				logger.Warn(msg);
				return;
			}

			CrossRef_AniDB_MAL xref = new CrossRef_AniDB_MAL();
			xref.AnimeID = animeID;
			xref.MALID = malID;
			xref.MALTitle = malTitle;
			if (fromWebCache)
				xref.CrossRefSource = (int)CrossRefSource.WebCache;
			else
				xref.CrossRefSource = (int)CrossRefSource.User;

			repCrossRef.Save(xref);

			StatsCache.Instance.UpdateUsingAnime(animeID);

			logger.Trace("Changed MAL association: {0}", animeID);

			CommandRequest_WebCacheSendXRefAniDBMAL req = new CommandRequest_WebCacheSendXRefAniDBMAL(xref.CrossRef_AniDB_MALID);
			req.Save();
		}

		public static void RemoveLinkAniDBMAL(AnimeSeries ser)
		{
			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			CrossRef_AniDB_MAL xref = repCrossRef.GetByAnimeID(ser.AniDB_ID);
			if (xref == null) return;

			repCrossRef.Delete(xref.CrossRef_AniDB_MALID);

			CommandRequest_WebCacheDeleteXRefAniDBMAL req = new CommandRequest_WebCacheDeleteXRefAniDBMAL(ser.AniDB_ID);
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
				if (anime.CrossRefMAL != null) continue;

				logger.Trace(string.Format("Found anime without MAL association: {0} ({1})", anime.AnimeID, anime.MainTitle));

				CommandRequest_MALSearchAnime cmd = new CommandRequest_MALSearchAnime(ser.AniDB_ID, false);
				cmd.Save();
			}

		}
	}
}
