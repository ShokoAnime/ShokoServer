using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class UTorrentHelper
    {
        private string token = "";

        public bool Initialised { get; set; }

        CookieContainer cookieJar = null;

        private const string urlTorrentList = "http://{0}:{1}/gui/?token={2}&list=1";
        private const string urlTorrentFileList = "http://{0}:{1}/gui/?token={2}&action=getfiles&hash={3}";

        private const string urlTorrentTokenPage = "http://{0}:{1}/gui/token.html";
        private const string urlTorrentStart = "http://{0}:{1}/gui/?token={2}&action=start&hash={3}";
        private const string urlTorrentStop = "http://{0}:{1}/gui/?token={2}&action=stop&hash={3}";
        private const string urlTorrentPause = "http://{0}:{1}/gui/?token={2}&action=pause&hash={3}";
        private const string urlTorrentAddURL = "http://{0}:{1}/gui/?token={2}&action=add-url&s={3}";
        private const string urlTorrentRemove = "http://{0}:{1}/gui/?token={2}&action=remove&hash={3}";
        private const string urlTorrentRemoveData = "http://{0}:{1}/gui/?token={2}&action=removedata&hash={3}";
        private const string urlTorrentFilePriority = "http://{0}:{1}/gui/?token={2}&action=setprio&hash={3}&p={4}&f={5}";

        private System.Timers.Timer torrentsTimer = null;

        public delegate void ListRefreshedEventHandler(ListRefreshedEventArgs ev);
        public event ListRefreshedEventHandler ListRefreshedEvent;

        public delegate void InfoEventHandler(string data);

        public event InfoEventHandler InfoEvent;

        protected void OnListRefreshedEvent(ListRefreshedEventArgs ev)
        {
            ListRefreshedEvent?.Invoke(ev);
        }

        protected void OnInfoEvent(string data)
        {
            InfoEvent?.Invoke(data);
        }
        public UTorrentHelper()
        {
            Initialised = false;
        }

        public void Init()
        {
            OnInfoEvent("Populating security token...");
            PopulateToken();

            // timer for automatic updates
            torrentsTimer = new System.Timers.Timer();
            torrentsTimer.AutoReset = false;
            torrentsTimer.Interval = TorrentSettings.Instance.UTorrentRefreshInterval * 1000; // 5 seconds
            torrentsTimer.Elapsed += new System.Timers.ElapsedEventHandler(torrentsTimer_Elapsed);

            if (ValidCredentials())
            {
                // get the intial list of completed torrents
                List<Torrent> torrents = new List<Torrent>();
                bool success = GetTorrentList(ref torrents);

                torrentsTimer.Start();
                Initialised = true;
            }
        }

        void torrentsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {

            try
            {
                if (!TorrentSettings.Instance.UTorrentAutoRefresh) return;

                torrentsTimer.Stop();

                List<Torrent> torrents = new List<Torrent>();

                bool success = GetTorrentList(ref torrents);

                if (success)
                {
                    //OnListRefreshedEvent(new ListRefreshedEventArgs(torrents));
                    torrentsTimer.Interval = TorrentSettings.Instance.UTorrentRefreshInterval * 1000;
                }
                else
                    torrentsTimer.Interval = 60 * 1000;

                torrentsTimer.Start();
            }
            catch (Exception ex)
            {
                torrentsTimer.Start();
            }

        }

        public bool ValidCredentials()
        {
            if (TorrentSettings.Instance.UTorrentAddress.Trim().Length == 0) return false;
            if (TorrentSettings.Instance.UTorrentPort.Trim().Length == 0) return false;
            if (TorrentSettings.Instance.UTorrentUsername.Trim().Length == 0) return false;
            if (TorrentSettings.Instance.UTorrentPassword.Trim().Length == 0) return false;

            return true;
        }

        private void PopulateToken()
        {
            cookieJar = new CookieContainer();
            token = "";

            if (!ValidCredentials())
            {
                return;
            }

            string url = "";
            try
            {

                url = string.Format(urlTorrentTokenPage, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort);
                
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
                webReq.Timeout = 10000; // 10 seconds
                webReq.Credentials = new NetworkCredential(TorrentSettings.Instance.UTorrentUsername, TorrentSettings.Instance.UTorrentPassword);
                webReq.CookieContainer = cookieJar;

                HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();


                Stream responseStream = WebResponse.GetResponseStream();
                StreamReader Reader = new StreamReader(responseStream, Encoding.UTF8);

                string output = Reader.ReadToEnd();
                
                WebResponse.Close();
                responseStream.Close();

                // parse and get the token
                // <html><div id='token' style='display:none;'>u3iiuDG4dwYDMzurIFif7FS-ldLPcvHk6QlB4y8LSKK5mX9GSPUZ_PpxD0s=</div></html>

                char q = (char)34;
                string quote = q.ToString();

                string torStart = "display:none;'>";
                string torEnd = "</div>";

                int posTorStart = output.IndexOf(torStart, 0);
                if (posTorStart <= 0) return;

                int posTorEnd = output.IndexOf(torEnd, posTorStart + torStart.Length + 1);

                token = output.Substring(posTorStart + torStart.Length, posTorEnd - posTorStart - torStart.Length);
                //BaseConfig.MyAnimeLog.Write("token: {0}", token);
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void RemoveTorrent(string hash)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentRemove, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void RemoveTorrentAndData(string hash)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentRemoveData, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void AddTorrentFromURL(string downloadURL)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string encodedURL = HttpUtility.UrlEncode(downloadURL);
                string url = string.Format(urlTorrentAddURL, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, encodedURL);


                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void StopTorrent(string hash)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentStop, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void StartTorrent(string hash)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentStart, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public void PauseTorrent(string hash)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentPause, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        private string GetWebResponse(string url)
        {
            HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.Timeout = 15000; // 15 seconds
            webReq.Credentials = new NetworkCredential(TorrentSettings.Instance.UTorrentUsername, TorrentSettings.Instance.UTorrentPassword);
            webReq.CookieContainer = cookieJar;

            bool tryAgain = false;
            HttpWebResponse webResponse = null;
            try
            {
                webResponse = (HttpWebResponse)webReq.GetResponse();
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("(400) Bad Request"))
                {
                    tryAgain = true;
                }
            }

            if (tryAgain)
            {
                PopulateToken();

                // fin the token in the url and replace it with the new one
                //http://{0}:{1}/gui/?token={2}&list=1
                int iStart = url.IndexOf(@"?token=", 0);
                int iFinish = url.IndexOf(@"&", 0);

                string prefix = url.Substring(0, iStart);
                string tokenStr = @"?token=" + token;
                string suffix = url.Substring(iFinish, url.Length - iFinish);


                url = prefix + tokenStr + suffix;


                webReq = (HttpWebRequest)WebRequest.Create(url);
                webReq.Timeout = 15000; // 15 seconds
                webReq.Credentials = new NetworkCredential(TorrentSettings.Instance.UTorrentUsername, TorrentSettings.Instance.UTorrentPassword);
                webReq.CookieContainer = cookieJar;
                webResponse = (HttpWebResponse)webReq.GetResponse();
            }

            if (webResponse == null) return "";

            Stream responseStream = webResponse.GetResponseStream();
            StreamReader Reader = new StreamReader(responseStream, Encoding.UTF8);

            string output = Reader.ReadToEnd();

            webResponse.Close();
            responseStream.Close();

            return output;
        }

        public bool GetTorrentList(ref List<Torrent> torrents)
        {
            torrents = new List<Torrent>();

            if (!ValidCredentials())
            {
                return false;
            }

            string url = "";
            try
            {
                OnInfoEvent("Getting torrent list...");
                //http://[IP]:[PORT]/gui/?list=1
                url = string.Format(urlTorrentList, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token);
                string output = GetWebResponse(url);
                if (output.Length == 0)
                {
                    OnInfoEvent("Error!");
                    return false;
                }


                //BaseConfig.MyAnimeLog.Write("Torrent List JSON: {0}", output);
                TorrentList torList = JsonConvert.DeserializeObject<TorrentList>(output);

                foreach (object[] obj in torList.torrents)
                {
                    Torrent tor = new Torrent(obj);
                    torrents.Add(tor);
                }

                OnListRefreshedEvent(new ListRefreshedEventArgs(torrents));
                OnInfoEvent("Connected.");
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool GetFileList(string hash, ref List<TorrentFile> torFiles)
        {
            torFiles = new List<TorrentFile>();

            if (!ValidCredentials())
            {
               return false;
            }

            try
            {
                string url = string.Format(urlTorrentFileList, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash);
                string output = GetWebResponse(url);
                if (output.Length == 0) return false;

                TorrentFileList fileList = JsonConvert.DeserializeObject<TorrentFileList >(output);

                if (fileList != null && fileList.files != null && fileList.files.Length > 1)
                {
                    object[] actualFiles = fileList.files[1] as object[];
                    if (actualFiles == null) return false;

                    foreach (object obj in actualFiles)
                    {
                        object[] actualFile = obj as object[];
                        if (actualFile == null) continue;

                        TorrentFile tf = new TorrentFile();
                        tf.FileName = actualFile[0].ToString();
                        tf.FileSize = long.Parse(actualFile[1].ToString());
                        tf.Downloaded = long.Parse(actualFile[2].ToString());
                        tf.Priority = long.Parse(actualFile[3].ToString());

                        torFiles.Add(tf);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public void FileSetPriority(string hash, int idx, TorrentFilePriority priority)
        {
            if (!ValidCredentials())
            {
                return;
            }

            try
            {
                string url = string.Format(urlTorrentFilePriority, TorrentSettings.Instance.UTorrentAddress, TorrentSettings.Instance.UTorrentPort, token, hash, (int)priority, idx);
                string output = GetWebResponse(url);

                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }
    }
}
