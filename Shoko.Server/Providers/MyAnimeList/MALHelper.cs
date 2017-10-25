using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using AniDBAPI;
using Shoko.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.WebCache;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.MyAnimeList
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

            string searchResultXML = string.Empty;
            try
            {
                searchResultXML =
                    SendMALAuthenticatedRequest("https://myanimelist.net/api/anime/search.xml?q=" + searchTitle);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
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
                        animeEntry entry = new animeEntry
                        {
                            // default values
                            end_date = string.Empty,
                            english = string.Empty,
                            episodes = 0,
                            id = 0,
                            image = string.Empty,
                            score = 0,
                            start_date = string.Empty,
                            status = string.Empty,
                            synonyms = string.Empty,
                            synopsis = string.Empty,
                            title = string.Empty,
                            type = string.Empty
                        };
                        entry.end_date = AniDBHTTPHelper.TryGetProperty(node, "end_date");
                        entry.english = AniDBHTTPHelper.TryGetProperty(node, "english");
                        entry.image = AniDBHTTPHelper.TryGetProperty(node, "image");

                        int.TryParse(AniDBHTTPHelper.TryGetProperty(node, "episodes"), out int eps);
                        int.TryParse(AniDBHTTPHelper.TryGetProperty(node, "id"), out int id);
                        decimal.TryParse(AniDBHTTPHelper.TryGetProperty(node, "score"), out decimal score);

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
                        logger.Error(ex, ex.ToString());
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

        private static string SendMALAuthenticatedRequest(string url)
        {
            HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
            webReq.Timeout = 30 * 1000;
            webReq.Credentials = new NetworkCredential(ServerSettings.MAL_Username, ServerSettings.MAL_Password);
            webReq.PreAuthenticate = true;
            webReq.UserAgent = "api-jmm-7EC61C5283B99DC1CFE9A3730BF507CE";

            HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse();

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

                string url = "https://myanimelist.net/api/account/verify_credentials.xml";
                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 30 * 1000;
                webReq.Credentials = new NetworkCredential(username, password);
                webReq.PreAuthenticate = true;
                webReq.UserAgent = "api-jmm-7EC61C5283B99DC1CFE9A3730BF507CE";

                HttpWebResponse WebResponse = (HttpWebResponse) webReq.GetResponse();

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
                user _user = (user) obj;

                if (_user.username.ToUpper() == username.ToUpper())
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        public static string ReplaceEntityNamesByCharacter(string strInput)
        {
            //entity characters
            string[] entityCharacters =
            {
                "'",
                " ", "¡", "¢", "£", "¤", "¥", "¦", "§",
                "¨", "©", "ª", "«", "¬", "­", "®", "¯", "°", "±", "²", "³", "´",
                "µ", "¶", "·", "¸", "¹", "º", "»", "¼", "½", "¾", "¿", "×", "÷",
                "À", "Á", "Â", "Ã", "Ä", "Å", "Æ", "Ç", "È", "É", "Ê", "Ë", "Ì",
                "Í", "Î", "Ï", "Ð", "Ñ", "Ò", "Ó", "Ô", "Õ", "Ö", "Ø", "Ù", "Ú",
                "Û", "Ü", "Ý", "Þ", "ß", "à", "á", "â", "ã", "ä", "å", "æ", "ç",
                "è", "é", "ê", "ë", "ì", "í", "î", "ï", "ð", "ñ", "ò", "ó", "ô",
                "õ", "ö", "ø", "ù", "ú", "û", "ü", "ý", "þ", "ÿ"
            };
            //entity names to be replaced by entity characters
            string[] entityNames =
            {
                "&apos;",
                "&nbsp;", "&iexcl;",
                "&cent;", "&pound;", "&curren;", "&yen;", "&brvbar;", "&sect;", "&uml;",
                "&copy;", "&ordf;", "&laquo;", "&not;", "&shy;", "&reg;", "&macr;",
                "&deg;", "&plusmn;", "&sup2;", "&sup3;", "&acute;", "&micro;", "&para;",
                "&middot;", "&cedil;", "&sup1;", "&ordm;", "&raquo;", "&frac14;", "&frac12;",
                "&frac34;", "&iquest;", "&times;", "&divide;", "&Agrave;", "&Aacute;", "&Acirc;",
                "&Atilde;", "&Auml;", "&Aring;", "&AElig;", "&Ccedil;", "&Egrave;", "&Eacute;",
                "&Ecirc;", "&Euml;", "&Igrave;", "&Iacute;", "&Icirc;", "&Iuml;", "&ETH;",
                "&Ntilde;", "&Ograve;", "&Oacute;", "&Ocirc;", "&Otilde;", "&Ouml;", "&Oslash;",
                "&Ugrave;", "&Uacute;", "&Ucirc;", "&Uuml;", "&Yacute;", "&THORN;", "&szlig;",
                "&agrave;", "&aacute;", "&acirc;", "&atilde;", "&auml;", "&aring;", "&aelig;",
                "&ccedil;", "&egrave;", "&eacute;", "&ecirc;", "&euml;", "&igrave;", "&iacute;",
                "&icirc;", "&iuml;", "&eth;", "&ntilde;", "&ograve;", "&oacute;", "&ocirc;",
                "&otilde;", "&ouml;", "&oslash;", "&ugrave;", "&uacute;", "&ucirc;", "&uuml;",
                "&yacute;", "&thorn;", "&yuml;"
            };


            if (entityCharacters.Length != entityNames.Length)
                return strInput;

            for (int i = 0; i < entityNames.Length; i++)
            {
                strInput = strInput.Replace(entityNames[i], entityCharacters[i]);
            }

            return strInput;
        }

        public static void LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber,
            bool fromWebCache)
        {
            CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malID);
            if (xrefTemp != null)
            {
                string msg = string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1}", malID,
                    xrefTemp.AnimeID);
                logger.Warn(msg);
                return;
            }

            CrossRef_AniDB_MAL xref = new CrossRef_AniDB_MAL
            {
                AnimeID = animeID,
                MALID = malID,
                MALTitle = malTitle,
                StartEpisodeType = epType,
                StartEpisodeNumber = epNumber
            };
            if (fromWebCache)
                xref.CrossRefSource = (int) CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int) CrossRefSource.User;

            RepoFactory.CrossRef_AniDB_MAL.Save(xref);
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);


            logger.Trace("Changed MAL association: {0}", animeID);

            CommandRequest_MALUpdatedWatchedStatus cmd = new CommandRequest_MALUpdatedWatchedStatus(animeID);
            cmd.Save();

            CommandRequest_WebCacheSendXRefAniDBMAL req =
                new CommandRequest_WebCacheSendXRefAniDBMAL(xref.CrossRef_AniDB_MALID);
            req.Save();
        }

        public static void RemoveLinkAniDBMAL(int animeID, int epType, int epNumber)
        {
            CrossRef_AniDB_MAL xref = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeConstraint(animeID, epType, epNumber);
            if (xref == null) return;

            RepoFactory.CrossRef_AniDB_MAL.Delete(xref.CrossRef_AniDB_MALID);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            CommandRequest_WebCacheDeleteXRefAniDBMAL req = new CommandRequest_WebCacheDeleteXRefAniDBMAL(animeID,
                epType,
                epNumber);
            req.Save();
        }

        public static void ScanForMatches()
        {
            if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
            {
                logger.Warn("Won't SCAN MAL, MAL credentials not provided");
                return;
            }

            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

            foreach (SVR_AnimeSeries ser in allSeries)
            {
                SVR_AniDB_Anime anime = ser.GetAnime();
                if (anime == null) continue;

                if (anime.IsMALLinkDisabled()) continue;

                // don't scan if it is associated on the TvDB
                List<CrossRef_AniDB_MAL> xrefs = anime.GetCrossRefMAL();
                if (xrefs == null || xrefs.Count == 0)
                {
                    logger.Trace(string.Format("Found anime without MAL association: {0} ({1})", anime.AnimeID,
                        anime.MainTitle));

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
                if (string.IsNullOrEmpty(ServerSettings.MAL_Username) ||
                    string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    logger.Warn("Won't search MAL, MAL credentials not provided");
                    return null;
                }

                string url = string.Format("https://myanimelist.net/malappinfo.php?u={0}&status=all&type=anime",
                    ServerSettings.MAL_Username);
                string malAnimeListXML = SendMALAuthenticatedRequest(url);
                malAnimeListXML = ReplaceEntityNamesByCharacter(malAnimeListXML);

                XmlSerializer serializer = new XmlSerializer(typeof(myanimelist));
                XmlDocument docAnimeList = new XmlDocument();
                docAnimeList.LoadXml(malAnimeListXML);

                XmlNodeReader reader = new XmlNodeReader(docAnimeList.DocumentElement);
                object obj = serializer.Deserialize(reader);
                myanimelist malAnimeList = (myanimelist) obj;
                return malAnimeList;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
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
				logger.Error( ex,ex.ToString());
			}
		}*/

        public static void UpdateMALSeries(SVR_AnimeSeries ser)
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.MAL_Username) ||
                    string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    return;

                // Populate MAL animelist hashtable if isNeverDecreaseWatched set
                Hashtable animeListHashtable = new Hashtable();
                myanimelist malAnimeList = GetMALAnimeList();

                if (ServerSettings.MAL_NeverDecreaseWatchedNums)
                    //if set, check watched number before update: take some time, as user anime list must be loaded
                {
                    if (malAnimeList != null && malAnimeList.anime != null)
                    {
                        for (int i = 0; i < malAnimeList.anime.Length; i++)
                        {
                            animeListHashtable.Add(malAnimeList.anime[i].series_animedb_id, malAnimeList.anime[i]);
                        }
                    }
                }

                // look for MAL Links
                List<CrossRef_AniDB_MAL> crossRefs = ser.GetAnime().GetCrossRefMAL();
                if (crossRefs == null || crossRefs.Count == 0)
                {
                    logger.Warn("Could not find MAL link for : {0} ({1})", ser.GetAnime().GetFormattedTitle(),
                        ser.GetAnime().AnimeID);
                    return;
                }

                List<SVR_AnimeEpisode> eps = ser.GetAnimeEpisodes();

                // find the anidb user
                List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
                if (aniDBUsers.Count == 0) return;

                SVR_JMMUser user = aniDBUsers[0];

                int score = 0;
                if (ser.GetAnime().UserVote != null)
                    score = ser.GetAnime().UserVote.VoteValue / 100;

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

                    foreach (SVR_AnimeEpisode ep in eps)
                    {
                        int epNum = ep.AniDB_Episode.EpisodeNumber;
                        if (xref.StartEpisodeType == (int) ep.EpisodeTypeEnum && epNum >= xref.StartEpisodeNumber &&
                            epNum <= GetUpperEpisodeLimit(crossRefs, xref))
                        {
                            malID = xref.MALID;
                            epNumber = epNum - xref.StartEpisodeNumber + 1;

                            // find the total episode count
                            if (totalEpCount < 0)
                            {
                                if (ep.EpisodeTypeEnum == EpisodeType.Episode)
                                    totalEpCount = ser.GetAnime().EpisodeCountNormal;
                                if (ep.EpisodeTypeEnum == EpisodeType.Special)
                                    totalEpCount = ser.GetAnime().EpisodeCountSpecial;
                                totalEpCount = totalEpCount - xref.StartEpisodeNumber + 1;
                            }

                            // any episodes here belong to the MAL series
                            // find the latest watched episod enumber
                            SVR_AnimeEpisode_User usrRecord = ep.GetUserRecord(user.JMMUserID);
                            if (usrRecord?.WatchedDate != null && epNum > lastWatchedEpNumber)
                            {
                                lastWatchedEpNumber = epNum;
                            }

                            List<CL_VideoDetailed> contracts = ep.GetVideoDetailedContracts(user.JMMUserID);

                            // find the latest episode number in the collection
                            if (contracts.Count > 0)
                                downloadedEps++;

                            foreach (CL_VideoDetailed contract in contracts)
                            {
                                if (!string.IsNullOrEmpty(contract.AniDB_Anime_GroupNameShort) &&
                                    !fanSubGroups.Contains(contract.AniDB_Anime_GroupNameShort))
                                    fanSubGroups.Add(contract.AniDB_Anime_GroupNameShort);
                            }
                        }
                    }

                    string fanSubs = string.Empty;
                    foreach (string fgrp in fanSubGroups)
                    {
                        if (!string.IsNullOrEmpty(fanSubs)) fanSubs += ",";
                        fanSubs += fgrp;
                    }

                    // determine status
                    int status = 1; //watching
                    int lastWatchedEpNumberMAL = 0;
                    if (animeListHashtable.ContainsKey(malID))
                    {
                        myanimelistAnime animeInList = (myanimelistAnime) animeListHashtable[malID];
                        status = animeInList.my_status;
                        lastWatchedEpNumberMAL = animeInList.my_watched_episodes;
                    }

                    // over-ride is user has watched an episode
                    // don't override on hold (3) or dropped (4) but do override plan to watch (6)
                    if (status == 6 && lastWatchedEpNumber > 0) status = 1; //watching
                    if (lastWatchedEpNumber == totalEpCount) status = 2; //completed

                    if (lastWatchedEpNumber > totalEpCount)
                    {
                        logger.Error("updateMAL, episode number > matching anime episode total : {0} ({1}) / {2}",
                            ser.GetAnime().GetFormattedTitle(), ser.GetAnime().AnimeID, epNumber);
                        continue;
                    }

                    if (malID <= 0 || totalEpCount <= 0)
                    {
                        logger.Warn("Could not find MAL link for : {0} ({1})", ser.GetAnime().GetFormattedTitle(),
                            ser.GetAnime().AnimeID);
                        continue;
                    }

                    string confirmationMessage = string.Empty;
                    if (animeListHashtable.ContainsKey(malID))
                    {
                        if ((ServerSettings.MAL_NeverDecreaseWatchedNums && lastWatchedEpNumberMAL > 0) && lastWatchedEpNumber <= lastWatchedEpNumberMAL)
                        {
                            continue;
                        }

                        ModifyAnime(malID, lastWatchedEpNumber, status, score, downloadedEps, fanSubs);
                        confirmationMessage = string.Format(
                            "MAL successfully updated (MAL MODIFY), mal id: {0}, ep: {1}, score: {2}", malID,
                            lastWatchedEpNumber, score);
                    }
                    else
                    {
                        AddAnime(malID, lastWatchedEpNumber, status, score, downloadedEps, fanSubs);
                        confirmationMessage = string.Format(
                            "MAL successfully updated (MAL ADD), mal id: {0}, ep: {1}, score: {2}", malID,
                            lastWatchedEpNumber, score);
                    }
                    logger.Trace(confirmationMessage);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
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


        public static bool AddAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps,
            string fanSubs)
        {
            try
            {
                string res = string.Empty;

                string animeValuesXMLString = string.Format(
                    "?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score></entry>",
                    lastEpisodeWatched, status, score);

                res =
                    SendMALAuthenticatedRequest("https://myanimelist.net/api/animelist/add/" + animeId + ".xml" +
                                                animeValuesXMLString);
                if (res.Contains("<title>201 Created</title>") == false)
                {
                    logger.Error("MAL AddAnime failed: " + res);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        public static bool ModifyAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps,
            string fanSubs)
        {
            try
            {
                string res = string.Empty;
                string animeValuesXMLString = string.Format(
                    "?data=<entry><episode>{0}</episode><status>{1}</status><score>{2}</score></entry>",
                    lastEpisodeWatched, status, score);

                res =
                    SendMALAuthenticatedRequest("https://myanimelist.net/api/animelist/update/" + animeId + ".xml" +
                                                animeValuesXMLString);
                if (res.Equals("Updated", StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    logger.Error("MAL ModifyAnime failed: " + res);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        // status: 1/watching, 2/completed, 3/onhold, 4/dropped, 6/plantowatch
        public static bool UpdateAnime(int animeId, int lastEpisodeWatched, int status, int score, int downloadedEps,
            string fanSubs)
        {
            // now modify back to proper status
            if (!AddAnime(animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs))
            {
                return ModifyAnime(animeId, lastEpisodeWatched, status, score, downloadedEps, fanSubs);
            }
            return true;
        }
    }

    internal class HtmlEntities
    {
        // Fields
        private static string[] _entitiesList = new string[]
        {
            "\"-quot", "&-amp", "<-lt", ">-gt", "\x00a0-nbsp", "\x00a1-iexcl", "\x00a2-cent", "\x00a3-pound",
            "\x00a4-curren",
            "\x00a5-yen", "\x00a6-brvbar", "\x00a7-sect", "\x00a8-uml", "\x00a9-copy", "\x00aa-ordf", "\x00ab-laquo",
            "\x00ac-not", "\x00ad-shy", "\x00ae-reg", "\x00af-macr", "\x00b0-deg", "\x00b1-plusmn", "\x00b2-sup2",
            "\x00b3-sup3",
            "\x00b4-acute", "\x00b5-micro", "\x00b6-para", "\x00b7-middot", "\x00b8-cedil", "\x00b9-sup1",
            "\x00ba-ordm",
            "\x00bb-raquo",
            "\x00bc-frac14", "\x00bd-frac12", "\x00be-frac34", "\x00bf-iquest", "\x00c0-Agrave", "\x00c1-Aacute",
            "\x00c2-Acirc",
            "\x00c3-Atilde", "\x00c4-Auml", "\x00c5-Aring", "\x00c6-AElig", "\x00c7-Ccedil", "\x00c8-Egrave",
            "\x00c9-Eacute",
            "\x00ca-Ecirc", "\x00cb-Euml",
            "\x00cc-Igrave", "\x00cd-Iacute", "\x00ce-Icirc", "\x00cf-Iuml", "\x00d0-ETH", "\x00d1-Ntilde",
            "\x00d2-Ograve",
            "\x00d3-Oacute", "\x00d4-Ocirc", "\x00d5-Otilde", "\x00d6-Ouml", "\x00d7-times", "\x00d8-Oslash",
            "\x00d9-Ugrave",
            "\x00da-Uacute", "\x00db-Ucirc",
            "\x00dc-Uuml", "\x00dd-Yacute", "\x00de-THORN", "\x00df-szlig", "\x00e0-agrave", "\x00e1-aacute",
            "\x00e2-acirc",
            "\x00e3-atilde", "\x00e4-auml", "\x00e5-aring", "\x00e6-aelig", "\x00e7-ccedil", "\x00e8-egrave",
            "\x00e9-eacute",
            "\x00ea-ecirc", "\x00eb-euml",
            "\x00ec-igrave", "\x00ed-iacute", "\x00ee-icirc", "\x00ef-iuml", "\x00f0-eth", "\x00f1-ntilde",
            "\x00f2-ograve",
            "\x00f3-oacute", "\x00f4-ocirc", "\x00f5-otilde", "\x00f6-ouml", "\x00f7-divide", "\x00f8-oslash",
            "\x00f9-ugrave",
            "\x00fa-uacute", "\x00fb-ucirc",
            "\x00fc-uuml", "\x00fd-yacute", "\x00fe-thorn", "\x00ff-yuml", "Œ-OElig", "œ-oelig", "Š-Scaron", "š-scaron",
            "Ÿ-Yuml",
            "ƒ-fnof", "ˆ-circ", "˜-tilde", "Α-Alpha", "Β-Beta", "Γ-Gamma", "Δ-Delta",
            "Ε-Epsilon", "Ζ-Zeta", "Η-Eta", "Θ-Theta", "Ι-Iota", "Κ-Kappa", "Λ-Lambda", "Μ-Mu", "Ν-Nu", "Ξ-Xi",
            "Ο-Omicron",
            "Π-Pi", "Ρ-Rho", "Σ-Sigma", "Τ-Tau", "Υ-Upsilon",
            "Φ-Phi", "Χ-Chi", "Ψ-Psi", "Ω-Omega", "α-alpha", "β-beta", "γ-gamma", "δ-delta", "ε-epsilon", "ζ-zeta",
            "η-eta",
            "θ-theta", "ι-iota", "κ-kappa", "λ-lambda", "μ-mu",
            "ν-nu", "ξ-xi", "ο-omicron", "π-pi", "ρ-rho", "ς-sigmaf", "σ-sigma", "τ-tau", "υ-upsilon", "φ-phi", "χ-chi",
            "ψ-psi",
            "ω-omega", "ϑ-thetasym", "ϒ-upsih", "ϖ-piv",
            " -ensp", " -emsp", " -thinsp", "‌-zwnj", "‍-zwj", "‎-lrm", "‏-rlm", "–-ndash", "—-mdash", "‘-lsquo",
            "’-rsquo",
            "‚-sbquo", "“-ldquo", "”-rdquo", "„-bdquo", "†-dagger",
            "‡-Dagger", "•-bull", "…-hellip", "‰-permil", "′-prime", "″-Prime", "‹-lsaquo", "›-rsaquo", "‾-oline",
            "⁄-frasl",
            "€-euro", "ℑ-image", "℘-weierp", "ℜ-real", "™-trade", "ℵ-alefsym",
            "←-larr", "↑-uarr", "→-rarr", "↓-darr", "↔-harr", "↵-crarr", "⇐-lArr", "⇑-uArr", "⇒-rArr", "⇓-dArr",
            "⇔-hArr",
            "∀-forall", "∂-part", "∃-exist", "∅-empty", "∇-nabla",
            "∈-isin", "∉-notin", "∋-ni", "∏-prod", "∑-sum", "−-minus", "∗-lowast", "√-radic", "∝-prop", "∞-infin",
            "∠-ang",
            "∧-and", "∨-or", "∩-cap", "∪-cup", "∫-int",
            "∴-there4", "∼-sim", "≅-cong", "≈-asymp", "≠-ne", "≡-equiv", "≤-le", "≥-ge", "⊂-sub", "⊃-sup", "⊄-nsub",
            "⊆-sube",
            "⊇-supe", "⊕-oplus", "⊗-otimes", "⊥-perp",
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
                return (char) obj2;
            }
            return '\0';
        }
    }

    public sealed class HttpUtilityV2
    {
        private static char[] s_entityEndingChars = new char[] {';', '&'};

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
                                string entity = s.Substring(i + 1, num3 - i - 1);
                                if ((entity.Length > 1) && (entity[0] == '#'))
                                {
                                    try
                                    {
                                        if ((entity[1] == 'x') || (entity[1] == 'X'))
                                        {
                                            ch = (char) int.Parse(entity.Substring(2), NumberStyles.AllowHexSpecifier);
                                        }
                                        else
                                        {
                                            ch = (char) int.Parse(entity.Substring(1));
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
                        Label_0103:
                        ;
                    }
                }
            }
        }
    }
}