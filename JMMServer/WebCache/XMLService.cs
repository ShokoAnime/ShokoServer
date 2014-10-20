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
