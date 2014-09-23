using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using System.IO;
using System.Net;
using JMMServer.Entities;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.AniDB_API;
using JMMServer.Repositories;

namespace JMMServer.WebCache
{
	public class XMLService
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();


		#region AniDB File

		public static AniDB_FileRequest Get_AniDB_File(string hash, long filesize)
		{
			// turn this off for now until the bugs are sorted out
			return null;

			if (!ServerSettings.WebCache_AniDB_File_Get) return null;

			try
			{

				string uri = string.Format("http://{0}/GetAniDB_File.aspx?hash={1}&fsize={2}",
					ServerSettings.WebCache_Address, hash, filesize);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlSerializer serializer = new XmlSerializer(typeof(AniDB_FileRequest));
				XmlDocument docSearchResult = new XmlDocument();
				docSearchResult.LoadXml(xml);

				XmlNodeReader reader = new XmlNodeReader(docSearchResult.DocumentElement);
				object obj = serializer.Deserialize(reader);
				return (AniDB_FileRequest)obj;

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_FileHash:: {0}", ex);
				return null;
			}
		}

		public static void Send_AniDB_File(AniDB_File data)
		{
			if (!ServerSettings.WebCache_AniDB_File_Send) return;


			string uri = string.Format("http://{0}/AddAniDB_File.aspx", ServerSettings.WebCache_Address);
			AniDB_FileRequest req = new AniDB_FileRequest(data);
			string xml = req.ToXML();

			SendData(uri, xml);
		}

		#endregion

		#region File Hash

		public static string Get_FileHash(string filename, long filesize)
		{
			if (!ServerSettings.WebCache_FileHashes_Get) return "";

			try
			{
				string fileName = Path.GetFileName(filename);

				fileName = fileName.Replace("+", "%252b");
				fileName = fileName.Replace("&", "%26");
				fileName = fileName.Replace("[", "%5b");
				fileName = fileName.Replace("]", "%5d");

				string username = ServerSettings.AniDB_Username;
				if (ServerSettings.WebCache_Anonymous)
					username = Constants.AnonWebCacheUsername;

				string uri = string.Format("http://{0}/GetFileNameHash.aspx?uname={1}&fname={2}&fsize={3}",
					ServerSettings.WebCache_Address, username, fileName, filesize);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return "";

				XmlDocument docFile = new XmlDocument();
				docFile.LoadXml(xml);

				string hash = TryGetProperty(docFile, "FileHashResult", "Hash").ToUpper();

				return hash;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_FileHash:: {0}", ex);
				return "";
			}
		}

		public static void Send_FileHash(VideoLocal data)
		{
			if (!ServerSettings.WebCache_FileHashes_Send) return;


			string uri = string.Format("http://{0}/AddFileNameHash.aspx", ServerSettings.WebCache_Address);
			FileHashRequest fhr = new FileHashRequest(data);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		#endregion

		#region CrossRef File Episode

		/// <summary>
		/// Current only handling one file containing file
		/// </summary>
		/// <param name="vid"></param>
		/// <returns></returns>
		public static List<CrossRef_File_Episode> Get_CrossRef_File_Episode(VideoLocal vid)
		{
			List<CrossRef_File_Episode> crossRefs = new List<CrossRef_File_Episode>();

			if (!ServerSettings.WebCache_XRefFileEpisode_Get) return null;

			try
			{
				string username = ServerSettings.AniDB_Username;
				if (ServerSettings.WebCache_Anonymous)
					username = Constants.AnonWebCacheUsername;

				string uri = string.Format("http://{0}/GetCrossRef_File_Episode.aspx?uname={1}&hash={2}",
					ServerSettings.WebCache_Address, username, vid.Hash);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlDocument docFile = new XmlDocument();
				docFile.LoadXml(xml);

				string sAnimeID = TryGetProperty(docFile, "CrossRef_File_EpisodeResultCollection", "AnimeID").ToUpper();
				string sEpisodeID = TryGetProperty(docFile, "CrossRef_File_EpisodeResultCollection", "EpisodeIDs").ToUpper();
				string sEpisodeOrder = TryGetProperty(docFile, "CrossRef_File_EpisodeResultCollection", "EpisodeOrders").ToUpper();
				string sPercentages = TryGetProperty(docFile, "CrossRef_File_EpisodeResultCollection", "EpisodePercentages").ToUpper();

				int animeID = 0;
				int.TryParse(sAnimeID, out animeID);

				if (animeID <= 0) return null;

				string[] epids = sEpisodeID.Split('|');
				string[] eporders = sEpisodeOrder.Split('|');
				string[] eppcts = sPercentages.Split('|');

				for (int i=0;i < epids.Length; i++)
				{
					int episodeID = 0, percentage = 0, episodeOrder = 0;

					int.TryParse(epids[i], out episodeID);
					int.TryParse(eporders[i], out episodeOrder);
					int.TryParse(eppcts[i], out percentage);

					CrossRef_File_Episode xref = new CrossRef_File_Episode();
					xref.Hash = vid.ED2KHash;
					xref.FileName = Path.GetFileName(vid.FullServerPath);
					xref.FileSize = vid.FileSize;
					xref.CrossRefSource = (int)JMMServer.CrossRefSource.WebCache;
					xref.AnimeID = animeID;
					xref.EpisodeID = episodeID;
					xref.Percentage = percentage;
					xref.EpisodeOrder = episodeOrder;

					crossRefs.Add(xref);
				}

				

				return crossRefs;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_FileHash:: {0}", ex);
				return null;
			}
		}

		public static void Send_CrossRef_File_Episode(CrossRef_File_Episode data)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;


			string uri = string.Format("http://{0}/AddCrossRef_File_Episode.aspx", ServerSettings.WebCache_Address);
			CrossRef_File_EpisodeRequest fhr = new CrossRef_File_EpisodeRequest(data);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		public static void Delete_CrossRef_File_Episode(string hash, int aniDBEpisodeID)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send && !ServerSettings.WebCache_XRefFileEpisode_Get) return;

			string uri = string.Format("http://{0}/DeleteCrossRef_File_Episode.aspx", ServerSettings.WebCache_Address);
			DeleteCrossRef_File_EpisodeRequest fhr = new DeleteCrossRef_File_EpisodeRequest(hash, aniDBEpisodeID);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		#endregion

		#region CrossRef AniDB to Other

		public static void Send_CrossRef_AniDB_Other(CrossRef_AniDB_Other data)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;


			string uri = string.Format("http://{0}/AddCrossRef_AniDB_Other.aspx", ServerSettings.WebCache_Address);
			AddCrossRef_AniDB_OtherRequest fhr = new AddCrossRef_AniDB_OtherRequest(data);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		public static void Delete_CrossRef_AniDB_Other(int animeID, CrossRefType xrefType)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
			if (ServerSettings.WebCache_Anonymous) return;

			string uri = string.Format("http://{0}/DeleteCrossRef_AniDB_Other.aspx", ServerSettings.WebCache_Address);
			DeleteCrossRef_AniDB_OtherRequest req = new DeleteCrossRef_AniDB_OtherRequest(animeID, xrefType);
			string xml = req.ToXML();

			SendData(uri, xml);
		}

		public static CrossRef_AniDB_OtherResult Get_CrossRef_AniDB_Other(int animeID, CrossRefType xrefType)
		{
			if (!ServerSettings.WebCache_TvDB_Get) return null;

			try
			{
				string username = ServerSettings.AniDB_Username;
				if (ServerSettings.WebCache_Anonymous)
					username = Constants.AnonWebCacheUsername;

				string uri = string.Format("http://{0}/GetCrossRef_AniDB_Other.aspx?uname={1}&AnimeID={2}&CrossRefType={3}",
					ServerSettings.WebCache_Address, username, animeID, (int)xrefType);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlDocument docFile = new XmlDocument();
				docFile.LoadXml(xml);

				string sOtherDBID = TryGetProperty(docFile, "CrossRef_AniDB_OtherResult", "CrossRefID");

				if (string.IsNullOrEmpty(sOtherDBID)) return null;

				CrossRef_AniDB_OtherResult result = new CrossRef_AniDB_OtherResult();
				result.AnimeID = animeID;
				result.CrossRefID = sOtherDBID;

				return result;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_FileHash:: {0}", ex);
				return null;
			}
		}

		#endregion

		#region CrossRef AniDB to Trakt

		public static void Send_CrossRef_AniDB_Trakt(CrossRef_AniDB_Trakt data, string showName)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;


			string uri = string.Format("http://{0}/AddCrossRef_AniDB_Trakt.aspx", ServerSettings.WebCache_Address);
			AddCrossRef_AniDB_TraktRequest fhr = new AddCrossRef_AniDB_TraktRequest(data, showName);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		public static void Delete_CrossRef_AniDB_Trakt(int animeID)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
			if (ServerSettings.WebCache_Anonymous) return;

			string uri = string.Format("http://{0}/DeleteCrossRef_AniDB_Trakt.aspx", ServerSettings.WebCache_Address);
			DeleteCrossRef_AniDB_TraktRequest req = new DeleteCrossRef_AniDB_TraktRequest(animeID);
			string xml = req.ToXML();

			SendData(uri, xml);
		}

		public static CrossRef_AniDB_TraktResult Get_CrossRef_AniDB_Trakt(int animeID)
		{
			if (!ServerSettings.WebCache_TvDB_Get) return null;

			try
			{
				string username = ServerSettings.AniDB_Username;
				if (ServerSettings.WebCache_Anonymous)
					username = Constants.AnonWebCacheUsername;

				string uri = string.Format("http://{0}/GetCrossRef_AniDB_Trakt.aspx?uname={1}&AnimeID={2}",
					ServerSettings.WebCache_Address, username, animeID);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlDocument docFile = new XmlDocument();
				docFile.LoadXml(xml);

				string sTraktID = TryGetProperty(docFile, "CrossRef_AniDB_TraktResult", "TraktID");
				string sTraktSeasonNumber = TryGetProperty(docFile, "CrossRef_AniDB_TraktResult", "TraktSeasonNumber");
				string sAdminApproved = TryGetProperty(docFile, "CrossRef_AniDB_TraktResult", "AdminApproved");
				string showName = TryGetProperty(docFile, "CrossRef_AniDB_TraktResult", "ShowName");


				int SeasonNumber = 0;
				int.TryParse(sTraktSeasonNumber, out SeasonNumber);

				int AdminApproved = 0;
				int.TryParse(sAdminApproved, out AdminApproved);

				CrossRef_AniDB_TraktResult result = new CrossRef_AniDB_TraktResult();
				result.AnimeID = animeID;
				result.TraktID = sTraktID;
				result.TraktSeasonNumber = SeasonNumber;
				result.ShowName = showName;

				return result;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_CrossRef_AniDB_Trakt:: {0}", ex);
				return null;
			}
		}

		#endregion

		#region CrossRef AniDB to MAL

		public static void Send_CrossRef_AniDB_MAL(CrossRef_AniDB_MAL data)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;


			string uri = string.Format("http://{0}/AddCrossRef_AniDB_MAL.aspx", ServerSettings.WebCache_Address);
			AddCrossRef_AniDB_MALRequest fhr = new AddCrossRef_AniDB_MALRequest(data);
			string xml = fhr.ToXML();

			SendData(uri, xml);
		}

		public static void Delete_CrossRef_AniDB_MAL(int animeID, int epType, int epNumber)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
			if (ServerSettings.WebCache_Anonymous) return;

			string uri = string.Format("http://{0}/DeleteCrossRef_AniDB_MAL.aspx", ServerSettings.WebCache_Address);
			DeleteCrossRef_AniDB_MALRequest req = new DeleteCrossRef_AniDB_MALRequest(animeID, epType, epNumber);
			string xml = req.ToXML();

			SendData(uri, xml);
		}

		public static List<CrossRef_AniDB_MALResult> Get_CrossRef_AniDB_MAL(int animeID)
		{
			if (!ServerSettings.WebCache_MAL_Get) return null;

			try
			{
				List<CrossRef_AniDB_MALResult> results = null;

				string username = ServerSettings.AniDB_Username;
				if (ServerSettings.WebCache_Anonymous)
					username = Constants.AnonWebCacheUsername;

				string uri = string.Format("http://{0}/GetCrossRef_AniDB_MAL.aspx?uname={1}&AnimeID={2}",
					ServerSettings.WebCache_Address, username, animeID);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlSerializer serializer = new XmlSerializer(typeof(List<CrossRef_AniDB_MALResult>));
				XmlDocument docSearchResult = new XmlDocument();
				docSearchResult.LoadXml(xml);

				XmlNodeReader reader = new XmlNodeReader(docSearchResult.DocumentElement);
				object obj = serializer.Deserialize(reader);
				results = (List<CrossRef_AniDB_MALResult>)obj;

				return results;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.Get_CrossRef_AniDB_MAL:: {0}", ex);
				return null;
			}
		}

		#endregion

		public static AppVersionsResult GetAppVersions()
		{
			try
			{
				AppVersionsResult appVersions = null;

				string uri = string.Format("http://{0}/GetAppVersions.aspx", ServerSettings.WebCache_Address);
				string xml = GetData(uri);

				if (xml.Trim().Length == 0) return null;

				XmlSerializer serializer = new XmlSerializer(typeof(AppVersionsResult));
				XmlDocument docSearchResult = new XmlDocument();
				docSearchResult.LoadXml(xml);

				XmlNodeReader reader = new XmlNodeReader(docSearchResult.DocumentElement);
				object obj = serializer.Deserialize(reader);
				appVersions = (AppVersionsResult)obj;

				return appVersions;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in XMLService.GetAppVersions:: {0}", ex);
				return null;
			}
		}

		private static string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
		{
			try
			{
				string prop = doc[keyName][propertyName].InnerText.Trim();
				return prop;
			}
			catch (Exception ex)
			{
				logger.Info("---------------------------------------------------------------");
				logger.Info("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
				logger.Info("keyName: {0}, propertyName: {1}", keyName, propertyName);
				logger.Info("---------------------------------------------------------------");
			}

			return "";
		}

		private static string GetData(string uri)
		{
			try
			{
				DateTime start = DateTime.Now;

				logger.Trace("GetData for: {0}", uri.ToString());
				string xml = Utils.DownloadWebPage(uri);
				TimeSpan ts = DateTime.Now - start;
				logger.Trace("GetData returned in {0}: {1} (in {2} ms)", Utils.GetParentMethodName(), xml, ts.TotalMilliseconds);
				if (xml.Contains(Constants.WebCacheError)) return "";

				return xml;
			}
			catch (WebException webEx)
			{
				logger.Error("Error(1) in XMLService.GetData: {0}", webEx);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error(2) in XMLService.GetData: {0}", ex);
			}

			return "";
		}

		private static void SendData(string uri, string xml)
		{

			WebRequest req = null;
			WebResponse rsp = null;
			try
			{
				DateTime start = DateTime.Now;


				req = WebRequest.Create(uri);
				req.Method = "POST";        // Post method
				req.ContentType = "text/xml";     // content type
				req.Proxy = null;

				// Wrap the request stream with a text-based writer
				StreamWriter writer = new StreamWriter(req.GetRequestStream());
				// Write the XML text into the stream
				writer.WriteLine(xml);
				writer.Close();
				// Send the data to the webserver
				rsp = req.GetResponse();

				TimeSpan ts = DateTime.Now - start;
				logger.Trace("Sent Web Cache Update in {0} ms: {1} --- {2}", ts.TotalMilliseconds, uri, xml);

			}
			catch (WebException webEx)
			{
				logger.Error("Error(1) in XMLServiceQueue.SendData: {0}", webEx);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error(2) in XMLServiceQueue.SendData: {0}", ex);
			}
			finally
			{
				if (req != null) req.GetRequestStream().Close();
				if (rsp != null) rsp.GetResponseStream().Close();
			}
		}
	}

}
