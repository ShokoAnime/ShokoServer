using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using NLog;
using System.Web;

namespace JMMServer.Providers.Azure
{
	public class AzureWebAPI
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static void Send_AnimeFull(JMMServer.Entities.AniDB_Anime data)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

			//string uri = string.Format(@"http://localhost:50994/api/animefull");
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/animefull");
			AnimeFull obj = data.ToContractAzure();
			string json = JSONHelper.Serialize<AnimeFull>(obj);
			SendData(uri, json, "POST");
		}

		public static void Send_AnimeXML(AnimeXML data)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

			//string uri = string.Format(@"http://localhost:50994/api/animexml");
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/animexml");
			string json = JSONHelper.Serialize<AnimeXML>(data);
			SendData(uri, json, "POST");
		}

		public static void Send_AnimeTitle(AnimeIDTitle data)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

			//string uri = string.Format(@"http://localhost:50994/api/animexml");
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/animeidtitle");
			string json = JSONHelper.Serialize<AnimeIDTitle>(data);
			SendData(uri, json, "POST");
		}

		public static List<AnimeIDTitle> Get_AnimeTitle(string query)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/animeidtitle/{0}", query);
			string msg = string.Format("Getting Anime Title Data From Cache: {0}", query);

			DateTime start = DateTime.Now;
			JMMService.LogToDatabase(Constants.DBLogType.APIAzureHTTP, msg);

			string json = GetDataJson(uri);

			TimeSpan ts = DateTime.Now - start;
			msg = string.Format("Got Anime Title Data From Cache: {0} - {1}", query, ts.TotalMilliseconds);
			JMMService.LogToDatabase(Constants.DBLogType.APIAzureHTTP, msg);

			List<AnimeIDTitle> titles = JSONHelper.Deserialize<List<AnimeIDTitle>>(json);

			return titles;
		}

		public static string Get_AnimeXML(int animeID)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;

			//string uri = string.Format(@"http://localhost:50994/api/animexml/{0}", animeID);
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/animexml/{0}", animeID);

			DateTime start = DateTime.Now;
			string msg = string.Format("Getting Anime XML Data From Cache: {0}", animeID);
			JMMService.LogToDatabase(Constants.DBLogType.APIAzureHTTP, msg);

			string xml = GetDataXML(uri);

			// remove the string container
			int iStart = xml.IndexOf("<?xml");
			if (iStart > 0)
			{
				string end = "</string>";
				int iEnd = xml.IndexOf(end);
				if (iEnd > 0)
				{
					xml = xml.Substring(iStart, iEnd - iStart -1);
				}
			}

			TimeSpan ts = DateTime.Now - start;
			string content = xml;
			if (content.Length > 100) content = content.Substring(0, 100);
			msg = string.Format("Got Anime XML Data From Cache: {0} - {1} - {2}", animeID, ts.TotalMilliseconds, content);
			JMMService.LogToDatabase(Constants.DBLogType.APIAzureHTTP, msg);

			return xml;
		}

		public static void Send_CrossRef_AniDB_MAL(JMMServer.Entities.CrossRef_AniDB_MAL data)
		{
			//if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;


			//string uri = string.Format(@"http://localhost:50994/api/CrossRef_AniDB_MAL");
			string uri = string.Format(@"http://jmm.azurewebsites.net/api/CrossRef_AniDB_MAL");
			CrossRef_AniDB_MAL fhr = data.ToContractAzure();
			string json = JSONHelper.Serialize<CrossRef_AniDB_MAL>(fhr);

			SendData(uri, json, "POST");
		}

		private static void SendData(string uri, string json, string verb)
		{

			WebRequest req = null;
			WebResponse rsp = null;
			try
			{
				DateTime start = DateTime.Now;

				req = WebRequest.Create(uri);
				//req.Method = "POST";        // Post method
				req.Method = verb;        // Post method, or PUT
				req.ContentType = "application/json; charset=UTF-8";     // content type
				req.Proxy = null;

				// Wrap the request stream with a text-based writer
				Encoding encoding = null;
				encoding = Encoding.UTF8;

				StreamWriter writer = new StreamWriter(req.GetRequestStream(), encoding);
				// Write the XML text into the stream
				writer.WriteLine(json);
				writer.Close();
				// Send the data to the webserver
				rsp = req.GetResponse();

				TimeSpan ts = DateTime.Now - start;
				logger.Trace("Sent Web Cache Update in {0} ms: {1}", ts.TotalMilliseconds, uri);

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

		private static string GetDataJson(string uri)
		{
			try
			{
				DateTime start = DateTime.Now;

				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(uri);
				webReq.Timeout = 60000; // 60 seconds
				webReq.Proxy = null;
				webReq.Method = "GET";
				webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
				webReq.ContentType = "application/json; charset=UTF-8";     // content type
				webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

				HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

				Stream responseStream = WebResponse.GetResponseStream();
				Encoding encoding = Encoding.UTF8; 
				StreamReader Reader = new StreamReader(responseStream, encoding);

				string output = Reader.ReadToEnd();
				output = HttpUtility.HtmlDecode(output);


				WebResponse.Close();
				responseStream.Close();

				return output;
			}
			catch (WebException webEx)
			{
				logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error(2) in AzureWebAPI.GetData: {0}", ex);
			}

			return "";
		}

		private static string GetDataXML(string uri)
		{
			try
			{
				DateTime start = DateTime.Now;

				HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(uri);
				webReq.Timeout = 60000; // 60 seconds
				webReq.Proxy = null;
				webReq.Method = "GET";
				webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
				webReq.ContentType = "text/xml";     // content type
				webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

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
				output = HttpUtility.HtmlDecode(output);


				WebResponse.Close();
				responseStream.Close();

				return output;
			}
			catch (WebException webEx)
			{
				logger.Error("Error(1) in AzureWebAPI.GetData: {0}", webEx);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error(2) in AzureWebAPI.GetData: {0}", ex);
			}

			return "";
		}

		/*public static void Delete_CrossRef_AniDB_MAL(int animeID, int epType, int epNumber)
		{
			if (!ServerSettings.WebCache_XRefFileEpisode_Send) return;
			if (ServerSettings.WebCache_Anonymous) return;

			string uri = string.Format("http://{0}/DeleteCrossRef_AniDB_MAL.aspx", ServerSettings.WebCache_Address);
			DeleteCrossRef_AniDB_MALRequest req = new DeleteCrossRef_AniDB_MALRequest(animeID, epType, epNumber);
			string xml = req.ToXML();

			SendData(uri, xml);
		}*/
	}
}
