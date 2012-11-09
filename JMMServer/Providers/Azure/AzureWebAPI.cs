using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using NLog;

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
				req.ContentType = "application/json";     // content type
				req.Proxy = null;

				// Wrap the request stream with a text-based writer
				StreamWriter writer = new StreamWriter(req.GetRequestStream());
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
