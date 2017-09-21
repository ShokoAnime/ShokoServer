using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Nancy.Routing.Trie.Nodes;
using NLog;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi.Plex;
using Shoko.Server.Repositories;
using Shoko.Server.API.v2.Models.common;
using Shoko.Server.Models;

namespace Shoko.Server.Commands.Plex
{
    internal class PlexHelper
    {
        internal JMMUser _user;


        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(GetPlexToken());
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private int _plexPinId = -1;

        private string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";

        private static readonly Dictionary<int, PlexHelper> Cache = new Dictionary<int, PlexHelper>();

        public static PlexHelper GetForUser(JMMUser user)
        {
            if (Cache.TryGetValue(user.JMMUserID, out PlexHelper helper)) return helper;
            Cache.Add(user.JMMUserID, helper = new PlexHelper(user));
            return helper;
        }

        private PlexHelper(JMMUser user)
        {
            this._user = user;
        }


        public PlexSeries[] GetPlexSeries(int section)
        {
            /*
             * if this doesn't work for all 
             **/
            XmlDocument xml = GetXml($"http://{ServerSettings.Plex_Server}/library/sections/{section}/all");
            XmlNodeList keys = xml.SelectNodes("/MediaContainer/Directory[@key]");

            if (keys == null)
                return new PlexSeries[0];

            List<PlexSeries> toReturn = new List<PlexSeries>();

            for (int i = 0; i < keys.Count; i++)
            {
                XmlAttributeCollection attributes = keys[i].Attributes;
                string key = attributes?["key"].Value;
                if (string.IsNullOrEmpty(key)) continue;


                XmlDocument doc = GetXml(
                    $"http://{ServerSettings.Plex_Server}{key.Substring(0, key.Length - 9)}/allLeaves");
                var files = doc.SelectNodes("/MediaContainer/Video");

                PlexSeries series = new PlexSeries()
                {
                    Episodes = new List<PlexEpisode>(),
                    Name = attributes?["title"].InnerText,
                    Season = Convert.ToInt32(attributes?["ratingKey"].InnerText)
                };

                for (int j = 0; j < files?.Count; j++)
                {
                    var videoXml = files[j];
                    string file = videoXml.SelectSingleNode("Media/Part")?.Attributes?["file"].InnerText;
                    int watchCount = Convert.ToInt32(videoXml.Attributes?["viewCount"]?.InnerText ?? "0");
                    int lastWatchedUnix = Convert.ToInt32(videoXml.Attributes?["lastViewedAt"]?.InnerText);
                    uint ratingKey = Convert.ToUInt32(videoXml.Attributes?["ratingKey"]?.InnerText);

                    series.Episodes.Add(new PlexEpisode()
                    {
                        File = file,
                        Key = ratingKey,
                        LastWatched = lastWatchedUnix,
                        WatchCount = watchCount,
                        Helper = this
                    });
                }

                toReturn.Add(series);
            }

            return toReturn.ToArray();
        }

        internal void RequestFromPlex(string url, string method = "GET", bool usePlexToken = true)
        {
            var req = CreateWebRequest(url, method, usePlexToken);

            WebResponse response = null;
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                response = (HttpWebResponse) req.GetResponse();
                stream = response.GetResponseStream();
                reader = new StreamReader(stream);

                // get the response
                var httpResponse = (HttpWebResponse) req.GetResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in {1}: {0}", ex, nameof(RequestFromPlex));
            }
            finally
            {
                stream?.Close();
                reader?.Close();
                response?.Close();
            }
        }

        internal XmlDocument GetXml(string url, string method = "GET", bool usePlexToken = true)
        {
            var req = CreateWebRequest(url, method, usePlexToken);

            WebResponse response = null;
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                response = (HttpWebResponse) req.GetResponse();
                stream = response.GetResponseStream();
                reader = new StreamReader(stream);

                // get the response
                var httpResponse = (HttpWebResponse) req.GetResponse();

                if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    return null;

                XmlDocument doc = new XmlDocument();
                doc.Load(reader);

                return doc;
            }
            catch (WebException e)
            {
                Logger.Error("Error in {1}: {0}", e, nameof(GetXml));

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("Error in {1}: {0}", ex, nameof(GetXml));
                return null;
            }
            finally
            {
                stream?.Close();
                reader?.Close();
                response?.Close();
            }
        }

        private HttpWebRequest CreateWebRequest(string url, string method = "GET", bool usePlexToken = true)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.KeepAlive = true;
            request.Method = method;
            request.Timeout = 120000;
            request.Accept = "application/xml";
            request.UserAgent = "JMM";

            request.Headers["X-Plex-Platform-Version"] = ServerState.Instance.ApplicationVersion;
            request.Headers["X-Plex-Platform"] = "Shoko Server";
            request.Headers["X-Plex-Device-Name"] = "Shoko Server Sync";
            request.Headers["X-Plex-Device"] = "Shoko";
            request.Headers["X-Plex-Client-Identifier"] = ClientIdentifier;
            if (usePlexToken)
                request.Headers["X-Plex-Token"] = GetPlexToken();

            return request;
        }

        public string Authenticate()
        {
            var doc = GetXml("https://plex.tv/pins.xml", "POST", false);
            _plexPinId = Convert.ToInt32(doc.SelectSingleNode("//pin/id")?.InnerText);
            var pin = doc.SelectSingleNode("//pin/code")?.InnerText;

            return pin;
        }

        public void InvalidateToken()
        {
            _user.PlexToken = string.Empty;
            new ShokoServiceImplementation().SaveUser(_user);
        }

        private string GetPlexToken()
        {
            if (!string.IsNullOrEmpty(_user.PlexToken))
                return _user.PlexToken;
            if (_plexPinId == -1) return null;

            var resp = GetXml($"https://plex.tv/pins/{_plexPinId}.xml", usePlexToken: false);
            if (resp == null)
                throw new PlexNotAuthedException($"Plex Pin: {_plexPinId}");
            _user.PlexToken = resp.SelectSingleNode("//pin/auth_token")?.InnerText;

            new ShokoServiceImplementation().SaveUser(_user);

            return _user.PlexToken;
        }
    }

    [Serializable]
    public class PlexNotAuthedException : Exception
    {
        public PlexNotAuthedException()
        {
        }

        public PlexNotAuthedException(string message) : base(message)
        {
        }

        public PlexNotAuthedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected PlexNotAuthedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    internal class PlexSeries
    {
        public List<PlexEpisode> Episodes { get; set; }
        public int Season { get; set; }
        public string Name { get; set; }
    }

    internal class PlexEpisode
    {
        public int WatchCount { get; set; }
        public long LastWatched { get; set; }
        public uint Key { get; set; }
        public string File { get; set; }
        internal PlexHelper Helper { get; set; }

        public SVR_AnimeEpisode AnimeEpisode => RepoFactory.AnimeEpisode.GetByFilename(Path.GetFileName(File));

        public void Unscrobble()
        {
            Helper.RequestFromPlex(
                $"http://{ServerSettings.Plex_Server}/:/unscrobble?identifier=com.plexapp.plugins.library&key={Key}");
        }

        public void Scrobble()
        {
            Helper.RequestFromPlex(
                $"http://{ServerSettings.Plex_Server}/:/scrobble?identifier=com.plexapp.plugins.library&key={Key}");
        }
    }
}