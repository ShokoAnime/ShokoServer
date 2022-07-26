using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Server;
using Shoko.Server.AniDB_API;
using Shoko.Server.AniDB_API.Raws;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace AniDBAPI
{
    public class AniDBHTTPHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public const string AnimeURL = @"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid={0}";

        public const string MyListURL = @"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={0}&pass={1}";

        public const string VotesURL = @"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=votes&user={0}&pass={1}";

        public static XmlDocument GetAnimeXMLFromAPI(int animeID)
        {
            try
            {
                if (ShokoService.AniDBProcessor.IsHttpBanned)
                {
                    logger.Info("GetAnimeXMLFromAPI: banned, not getting");
                    return null;
                }
                ShokoService.LastAniDBMessage = DateTime.Now;
                ShokoService.LastAniDBHTTPMessage = DateTime.Now;

                var anime = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);
                DateTime? prevUpdate = anime?.UpdatedAt;

                string uri = string.Format(AnimeURL, animeID);
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);
                DateTime start = DateTime.Now;
                string msg = string.Format(Resources.AniDB_GettingAnimeXML, animeID)+"; prevUpdate: "+prevUpdate;
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBHTTP, msg);

                string rawXML = DownloadWebPage(uri);

                // Putting this here for no chance of error. It is ALWAYS created or updated when AniDB is called!
                if (anime == null)
                    anime = new AniDB_AnimeUpdate {AnimeID = animeID, UpdatedAt = DateTime.Now};
                else
                    anime.UpdatedAt = DateTime.Now;
                RepoFactory.AniDB_AnimeUpdate.Save(anime);

                TimeSpan ts = DateTime.Now - start;
                string content = rawXML;
                if (content.Length > 100) content = content.Substring(0, 100);
                msg = string.Format(Resources.AniDB_GotAnimeXML, animeID, ts.TotalMilliseconds,
                    content);
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBHTTP, msg);

                XmlDocument docAnime = null;
                if (rawXML.Trim().Length > 0 && !CheckForBan(rawXML))
                {
                    var xmlUtils = ShokoServer.ServiceContainer.GetRequiredService<HttpXmlUtils>();
                    xmlUtils.WriteAnimeHTTPToFile(animeID, rawXML);

                    docAnime = new XmlDocument();
                    docAnime.LoadXml(rawXML);
                }
                else
                {
                    logger.Warn($"When downloading anime data for {animeID}, ban or no data was receved.");
                }

                return docAnime;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in AniDBHTTPHelper.GetAnimeXMLFromAPI: {0}");
                return null;
            }
        }

        public static string GetMyListXMLFromAPI(string username, string password)
        {
            try
            {
                if (ShokoService.AniDBProcessor.IsHttpBanned)
                {
                    logger.Info("GetMyListXMLFromAPI: banned, not getting");
                    return null;
                }
                ShokoService.LastAniDBMessage = DateTime.Now;
                ShokoService.LastAniDBHTTPMessage = DateTime.Now;

                string uri = string.Format(MyListURL, username, password);
                string rawXML = DownloadWebPage(uri);

                if (0 == rawXML.Trim().Length || CheckForBan(rawXML))
                    rawXML = null;

                return rawXML;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in AniDBHTTPHelper.GetMyListXMLFromAPI: {0}");
                return null;
            }
        }

        public static XmlDocument GetVotesXMLFromAPI(string username, string password)
        {
            try
            {
                if (ShokoService.AniDBProcessor.IsHttpBanned)
                {
                    logger.Info("GetVotesXMLFromAPI: banned, not getting");
                    return null;
                }
                ShokoService.LastAniDBMessage = DateTime.Now;
                ShokoService.LastAniDBHTTPMessage = DateTime.Now;

                string uri = string.Format(VotesURL, username, password);
                string rawXML = DownloadWebPage(uri);
                XmlDocument docAnime = null;
                if (0 < rawXML.Trim().Length && !CheckForBan(rawXML))
                {
                    docAnime = new XmlDocument();
                    docAnime.LoadXml(rawXML);
                }
                return docAnime;
            }
            catch
            {
                //BaseConfig.MyAnimeLog.Write("Error in AniDBHTTPHelper.GetAnimeXMLFromAPI: {0}", ex);
                return null;
            }
        }

        public static bool CheckForBan(string xmlresult)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(xmlresult))
            {
                int index = xmlresult.IndexOf(@">banned<", StringComparison.InvariantCultureIgnoreCase);
                if (-1 < index)
                {
                    logger.Warn("HTTP Banned!");
                    ShokoService.AniDBProcessor.IsHttpBanned = true;
                    result = true;
                }
            }
            return result;
        }

        public static Raw_AniDB_Anime ProcessAnimeDetails(XmlDocument docAnime, int animeID)
        {
            // most of the general anime data will be overwritten by the UDP command
            Raw_AniDB_Anime anime = new Raw_AniDB_Anime
            {
                AnimeID = animeID
            };

            // check if there is any data
            if (docAnime?["anime"]?.Attributes["id"]?.Value == null)
            {
                logger.Warn("AniDB ProcessAnimeDetails - Received no or invalid info in XML");
                return null;
            }

            anime.Description = TryGetProperty(docAnime, "anime", "description")?.Replace('`', '\'');
            anime.AnimeTypeRAW = TryGetProperty(docAnime, "anime", "type");


            string episodecount = TryGetProperty(docAnime, "anime", "episodecount");
            int.TryParse(episodecount, out int epCount);
            anime.EpisodeCount = epCount;
            anime.EpisodeCountNormal = epCount;

            string dateString = TryGetProperty(docAnime, "anime", "startdate");

            anime.AirDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out DateTime date))
                {
                    anime.AirDate = date;
                }
                else if (DateTime.TryParseExact(dateString, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out DateTime date2))
                {
                    anime.AirDate = date2;
                }
            }

            dateString = TryGetProperty(docAnime, "anime", "enddate");
            anime.EndDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out DateTime date))
                {
                    anime.EndDate = date;
                }
            }

            anime.BeginYear = anime.AirDate?.Year ?? 0;
            anime.EndYear = anime.EndDate?.Year ?? 0;

            //string enddate = TryGetProperty(docAnime, "anime", "enddate");

            string restricted = docAnime["anime"].Attributes["restricted"]?.Value;
            if (bool.TryParse(restricted, out bool res))
                anime.Restricted = res ? 1 : 0;
            else
                anime.Restricted = 0;

            anime.URL = TryGetProperty(docAnime, "anime", "url");
            anime.Picname = TryGetProperty(docAnime, "anime", "picture");

            anime.DateTimeUpdated = DateTime.Now;
            anime.DateTimeDescUpdated = anime.DateTimeUpdated;
            anime.ImageEnabled = 1;

            #region Related Anime

            XmlNodeList raItems = docAnime["anime"]["relatedanime"]?.GetElementsByTagName("anime");
            if (raItems != null)
            {
                anime.RelatedAnimeIdsRAW = string.Empty;
                anime.RelatedAnimeTypesRAW = string.Empty;

                foreach (XmlNode node in raItems)
                {
                    if (node?.Attributes?["id"]?.Value == null) continue;
                    if (!int.TryParse(node.Attributes["id"].Value, out int id)) continue;
                    int relType = ConvertReltTypeTextToEnum(TryGetAttribute(node, "type"));

                    if (anime.RelatedAnimeIdsRAW.Length > 0) anime.RelatedAnimeIdsRAW += "'";
                    if (anime.RelatedAnimeTypesRAW.Length > 0) anime.RelatedAnimeTypesRAW += "'";

                    anime.RelatedAnimeIdsRAW += id.ToString();
                    anime.RelatedAnimeTypesRAW += relType.ToString();
                }
            }

            #endregion

            #region Titles

            XmlNodeList titleItems = docAnime["anime"]["titles"]?.GetElementsByTagName("title");
            if (titleItems != null)
            {
                foreach (XmlNode node in titleItems)
                {
                    string titleType = node?.Attributes?["type"]?.Value?.Trim().ToLower();
                    if (string.IsNullOrEmpty(titleType)) continue;
                    string languageType = node.Attributes["xml:lang"]?.Value?.Trim().ToLower();
                    if (string.IsNullOrEmpty(languageType)) continue;
                    string titleValue = node.InnerText.Trim();
                    if (string.IsNullOrEmpty(titleValue)) continue;

                    if (titleType.Trim().ToUpper().Equals("MAIN"))
                        anime.MainTitle = titleValue.Replace('`', '\'');
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

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            XmlNodeList ratingItems = docAnime["anime"]["ratings"]?.ChildNodes;
            if (ratingItems == null) return anime;
            foreach (XmlNode node in ratingItems)
            {
                string name = node?.Name?.Trim().ToLower();
                if (string.IsNullOrEmpty(name)) continue;
                if (!int.TryParse(TryGetAttribute(node, "count"), out int iCount)) continue;
                if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out decimal iRating)) continue;
                iRating = (int) Math.Round(iRating * 100);

                if (name.Equals("permanent"))
                {
                    anime.VoteCount = iCount;
                    anime.Rating = (int) iRating;
                }
                else if (name.Equals("temporary"))
                {
                    anime.TempVoteCount = iCount;
                    anime.TempRating = (int) iRating;
                }
                else if (name.Equals("review"))
                {
                    anime.ReviewCount = iCount;
                    anime.AvgReviewRating = (int) iRating;
                }
            }

            #endregion

            return anime;
        }

        public static List<Raw_AniDB_ResourceLink> ProcessResources(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_ResourceLink> result = new List<Raw_AniDB_ResourceLink>();

            XmlNodeList items = docAnime?["anime"]?["resources"]?.GetElementsByTagName("resource");
            if (items == null) return result;
            foreach (XmlNode node in items) // each resource
            {
                try
                {
                    foreach (XmlNode child in node.ChildNodes) // each externalentity
                    {
                        Raw_AniDB_ResourceLink resource = new Raw_AniDB_ResourceLink();
                        resource.ProcessFromHTTPResult(node, animeID);
                        resource.RawID = child["identifier"]?.InnerText ?? child["url"]?.InnerText;
                        result.Add(resource);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error in AniDBHTTPHelper.ProcessResources: {ex}");
                }
            }

            return result;
        }

        public static List<Raw_AniDB_Tag> ProcessTags(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_Tag> tags = new List<Raw_AniDB_Tag>();

            XmlNodeList tagItems = docAnime?["anime"]?["tags"]?.GetElementsByTagName("tag");
            if (tagItems == null) return tags;
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
                    logger.Error(ex, $"Error in AniDBHTTPHelper.ProcessTags: {ex}");
                }
            }

            return tags;
        }

        public static List<Raw_AniDB_Staff> ProcessStaff(XmlDocument docAnime, int animeID)
        {
            var creators = new List<Raw_AniDB_Staff>();

            XmlNodeList charItems = docAnime?["anime"]?["creators"]?.GetElementsByTagName("name");
            if (charItems == null) return creators;
            foreach (XmlNode node in charItems)
            {
                try
                {
                    Raw_AniDB_Staff staff = new Raw_AniDB_Staff();
                    staff.ProcessFromHTTPResult(node, animeID);
                    creators.Add(staff);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error in AniDBHTTPHelper.ProcessCharacters: {ex}");
                }
            }
            return creators;
        }

        public static List<Raw_AniDB_Character> ProcessCharacters(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_Character> chars = new List<Raw_AniDB_Character>();

            XmlNodeList charItems = docAnime?["anime"]?["characters"]?.GetElementsByTagName("character");
            if (charItems == null) return chars;
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
                    logger.Error(ex, $"Error in AniDBHTTPHelper.ProcessCharacters: {ex}");
                }
            }

            return chars;
        }

        public static List<Raw_AniDB_Anime_Title> ProcessTitles(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_Anime_Title> titles = new List<Raw_AniDB_Anime_Title>();

            XmlNodeList titleItems = docAnime?["anime"]?["titles"]?.GetElementsByTagName("title");
            if (titleItems == null) return titles;
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
                    logger.Error(ex, $"Error in AniDBHTTPHelper.ProcessTitles: {animeID} - {ex}");
                }
            }

            return titles;
        }

        public static List<Raw_AniDB_RelatedAnime> ProcessRelations(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_RelatedAnime> rels = new List<Raw_AniDB_RelatedAnime>();

            XmlNodeList relItems = docAnime?["anime"]?["relatedanime"]?.GetElementsByTagName("anime");
            if (relItems == null) return rels;
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
                    logger.Error(ex, "Error in AniDBHTTPHelper.ProcessRelations: {0}");
                }
            }

            return rels;
        }

        public static List<Raw_AniDB_SimilarAnime> ProcessSimilarAnime(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_SimilarAnime> rels = new List<Raw_AniDB_SimilarAnime>();

            XmlNodeList simItems = docAnime["anime"]?["similaranime"]?.GetElementsByTagName("anime");
            if (simItems == null) return rels;
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
                    logger.Error(ex, "Error in AniDBHTTPHelper.ProcessSimilarAnime: {0}");
                }
            }

            return rels;
        }

        public static List<Raw_AniDB_Recommendation> ProcessRecommendations(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_Recommendation> recs = new List<Raw_AniDB_Recommendation>();

            XmlNodeList recItems = docAnime?["anime"]?["recommendations"]?.GetElementsByTagName("recommendation");
            if (recItems == null) return recs;
            foreach (XmlNode node in recItems)
            {
                try
                {
                    Raw_AniDB_Recommendation rec = new Raw_AniDB_Recommendation();
                    rec.ProcessFromHTTPResult(node, animeID);
                    recs.Add(rec);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error in Processing Node in Recommendations: {0}" + exc);
                }
            }

            return recs;
        }

        public static List<Raw_AniDB_Episode> ProcessEpisodes(XmlDocument docAnime, int animeID)
        {
            List<Raw_AniDB_Episode> eps = new List<Raw_AniDB_Episode>();

            XmlNodeList episodeItems = docAnime?["anime"]?["episodes"]?.GetElementsByTagName("episode");
            if (episodeItems == null) return eps;
            foreach (XmlNode node in episodeItems)
            {
                try
                {
                    Raw_AniDB_Episode ep = new Raw_AniDB_Episode();
                    if (!ep.ProcessEpisodeSource(node, animeID))
                    {
                        logger.Error($"AniDB Episode raw data had invalid return data:\n        {node}");
                        continue;
                    }
                    eps.Add(ep);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, $"Error in ProcessEpisodes: {exc}");
                }
            }

            return eps;
        }

        public static List<Raw_AniDB_MyListFile> ProcessMyList(XmlDocument docAnime)
        {
            List<Raw_AniDB_MyListFile> mylistentries = new List<Raw_AniDB_MyListFile>();

            XmlNodeList myitems = docAnime?["mylist"]?.GetElementsByTagName("mylistitem");

            if (myitems != null)
            {
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
                        logger.Error(ex, "Error in ProcessEpisodes: {0}" + ex);
                    }
                }
            }
            else
            {
                logger.Error("AniDB Sync_MyList - MyList xml is empty or invalid");
                return null;
            }

            return mylistentries;
        }

        public static List<Raw_AniDB_Vote_HTTP> ProcessVotes(XmlDocument docAnime)
        {
            List<Raw_AniDB_Vote_HTTP> myvotes = new List<Raw_AniDB_Vote_HTTP>();

            // get the permanent anime votes
            var myitems = docAnime?["votes"]?["anime"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                foreach (XmlNode node in myitems)
                {
                    Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
                    thisVote.ProcessAnime(node);
                    myvotes.Add(thisVote);
                }
            }

            // get the temporary anime votes
            myitems = docAnime?["votes"]?["animetemporary"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                foreach (XmlNode node in myitems)
                {
                    Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
                    thisVote.ProcessAnimeTemp(node);
                    myvotes.Add(thisVote);
                }
            }

            // get the episode votes
            myitems = docAnime?["votes"]?["episode"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                foreach (XmlNode node in myitems)
                {
                    Raw_AniDB_Vote_HTTP thisVote = new Raw_AniDB_Vote_HTTP();
                    thisVote.ProcessEpisode(node);
                    myvotes.Add(thisVote);
                }
            }

            return myvotes;
        }

        public static int ConvertReltTypeTextToEnum(string relType)
        {
            if (relType.Trim().ToLower().Equals("sequel")) return 1;
            if (relType.Trim().ToLower().Equals("prequel")) return 2;
            if (relType.Trim().ToLower().Equals("same setting")) return 11;
            if (relType.Trim().ToLower().Equals("alternative setting")) return 21;
            if (relType.Trim().ToLower().Equals("alternative version")) return 32;
            if (relType.Trim().ToLower().Equals("music video")) return 41;
            if (relType.Trim().ToLower().Equals("character")) return 42;
            if (relType.Trim().ToLower().Equals("side story")) return 51;
            if (relType.Trim().ToLower().Equals("parent story")) return 52;
            if (relType.Trim().ToLower().Equals("summary")) return 61;
            if (relType.Trim().ToLower().Equals("full story")) return 62;

            return 100;
        }

        public static string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
        {
            if (doc == null || string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(propertyName)) return string.Empty;
            return doc[keyName]?[propertyName]?.InnerText.Trim() ?? string.Empty;
        }

        public static string TryGetProperty(XmlNode node, string propertyName)
        {
            if (node == null || string.IsNullOrEmpty(propertyName)) return string.Empty;
            return node[propertyName]?.InnerText.Trim() ?? string.Empty;
        }


        public static string TryGetPropertyWithAttribute(XmlNode node, string propertyName, string attName,
            string attValue)
        {
            if (node == null || string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(attValue))
                return string.Empty;
            foreach (XmlNode nodeChild in node.ChildNodes)
            {
                if ((nodeChild?.Name.Equals(propertyName) ?? false) &&
                    (nodeChild.Attributes?[attName]?.Value.Equals(attValue) ?? false) &&
                    !string.IsNullOrEmpty(nodeChild.InnerText))
                    return nodeChild.InnerText.Trim();
            }
            return string.Empty;
        }

        public static string TryGetAttribute(XmlNode parentnode, string nodeName, string attName)
        {
            if (parentnode == null || string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(attName))
                return string.Empty;
            return parentnode[nodeName]?.Attributes[attName]?.Value ?? string.Empty;
        }

        public static string TryGetAttribute(XmlNode node, string attName)
        {
            if (node == null || string.IsNullOrEmpty(attName)) return string.Empty;
            return node.Attributes?[attName]?.Value ?? string.Empty;
        }
        
        public static string DownloadWebPage(string url)
        {
            try
            {
                StaticRateLimiter.HTTP.EnsureRate();

                var webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 20000; // 20 seconds
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var webResponse = (HttpWebResponse) webReq.GetResponse())
                {
                    if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentLength == 0)
                        throw new Exception("Response Body was expected, but none returned");
                    using (var responseStream = webResponse.GetResponseStream())
                    {
                        if (responseStream == null)
                            throw new Exception("Response Body was expected, but none returned");
                        var charset = webResponse.CharacterSet;
                        Encoding encoding = null;
                        if (!string.IsNullOrEmpty(charset))
                            encoding = Encoding.GetEncoding(charset);
                        if (encoding == null)
                            encoding = Encoding.UTF8;
                        var reader = new StreamReader(responseStream, encoding);

                        var output = reader.ReadToEnd();
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in APIUtils.DownloadWebPage: {0}", ex);
                return string.Empty;
            }
        }
    }
}
