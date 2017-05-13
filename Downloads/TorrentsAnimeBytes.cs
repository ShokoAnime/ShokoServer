using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using Shoko.Commons.Languages;

using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class TorrentsAnimeBytes : ITorrentSource
    {
        private TorrentSourceType SourceType = TorrentSourceType.AnimeBytes;

        #region ITorrentSource Members

        public string TorrentSourceName => EnumTranslator.TorrentSourceTranslated(SourceType);

        public string TorrentSourceNameShort => EnumTranslator.TorrentSourceTranslatedShort(SourceType);

        public string GetSourceName()
        {
            return TorrentSourceName;
        }

        public string GetSourceNameShort()
        {
            return TorrentSourceNameShort;
        }

        public bool SupportsSearching()
        {
            return true;
        }

        public bool SupportsBrowsing()
        {
            return true;
        }

        public bool SupportsCRCMatching()
        {
            return true;
        }


        public string Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return "";

            try
            {
                WebClientEx client = null;
                while (client == null)
                {
                    Console.WriteLine("Trying..");
                    client = CloudflareEvader.CreateBypassedWebClient("https://animebytes.tv");
                }

                try
                {
                    using (client)
                    {
                        var values = new NameValueCollection
                    {
                        { "username", username },
                        { "password", password },
                    };
                        // Authenticate
                        client.UploadValues("https://animebytes.tv/user/login", values);
                        // Download desired page

                        string temp = client.CookieContainer.GetCookieHeader(new Uri("http://animebytes.tv/index.php"));

                        if (ValidateCookie(temp))
                            return client.CookieContainer.GetCookieHeader(new Uri("http://animebytes.tv/index.php"));
                        else
                            return "";
                    }

                }
                catch
                {
                    return "";
                }

                //Console.WriteLine("Solved! We're clear to go");
                //Console.WriteLine(client.DownloadString("http://anilinkz.tv/anime-list"));

            }
            catch
            {
                return "";
            }
        }

        private bool ValidateCookie(string cookie)
        {
            string url = "http://animebytes.tv/torrents.php?filter_cat[1]=1&sort=time_added&way=desc";

            string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, cookie, true);

            return ParseSource(output).Count > 1;
        }

        private List<TorrentLink> ParseSource(string output)
        {
            List<TorrentLink> torLinks = new List<TorrentLink>();

            char q = (char)34;
            string quote = q.ToString();

            string nameStart = "<a href=" + quote + "/series.php?id=";
            string nameStart2 = quote + ">";
            string nameEnd = "</a>";

            // title="View Torrent">
            string typeStart = "title=" + quote + "View Torrent" + quote + ">";
            string typeEnd = "</a>";

            // <tr class="edition_info"><td class=" " colspan="5"><strong>
            //string torrentSectionStart = "<tr class=" + quote + "edition_info";
            // <table class="torrent_group">
            string torrentSectionStart = "<table class=" + quote + "torrent_group";
            // </table>
            string torrentSectionEnd = "</table>";

            // <a href="torrents.php?action=download
            string torDownloadStart = "<a href=" + quote + "torrents.php?action=download";
            string torDownloadEnd = quote;

            // <a href="torrents.php?id=
            string torInfoPreStart = "<a href=" + quote + "torrents.php?id=";
            string torInfoStart = ">";
            string torInfoEnd = "</a>";

            // <td class="torrent_size"><span>
            string torSizeStart = "<td class=" + quote + "torrent_size" + quote + "><span>";
            string torSizeEnd = "</span>";

            // <td class="torrent_seeders" title="Seeders"><span>
            string torSeedStart = "<td class=" + quote + "torrent_seeders" + quote + " title=" + quote + "Seeders" + quote + "><span>";
            string torSeedEnd = "</span>";

            // <td class="torrent_leechers" title="Leechers"><span>
            string torLeechStart = "<td class=" + quote + "torrent_leechers" + quote + " title=" + quote + "Leechers" + quote + "><span>";
            string torLeechEnd = "</span>";

            // <img src="static/common/hentaic.png" alt="Hentai" title="This torrent is of censored hentai (18+) material!" />
            // <img src="static/common/flicon.png" alt="Freeleech!" title="This torrent is freeleech. Remember to seed!" />
            string hentaiTag = "alt=" + quote + "Hentai";
            string leechTag = "alt=" + quote + "Freeleech";

            int pos = output.IndexOf(nameStart, 0);
            while (pos > 0)
            {

                if (pos <= 0) break;

                //int posnameStart = output.IndexOf(nameStart, pos + 1);
                int posnameStart2 = output.IndexOf(nameStart2, pos + nameStart.Length);
                int posnameEnd = output.IndexOf(nameEnd, posnameStart2 + nameStart2.Length + 1);

                string torName = output.Substring(posnameStart2 + nameStart2.Length, posnameEnd - posnameStart2 - nameStart2.Length);

                // remove html codes
                torName = HttpUtility.HtmlDecode(torName);

                int posTypeStart = output.IndexOf(typeStart, posnameEnd + 1);
                int posTypeEnd = output.IndexOf(typeEnd, posTypeStart + 1);

                string torType = output.Substring(posTypeStart + typeStart.Length, posTypeEnd - posTypeStart - typeStart.Length);

                // get all the torrents

                // find the section start and end
                int posTorSectionStart = output.IndexOf(torrentSectionStart, posTypeEnd + 1);
                int posTorSectionEnd = output.IndexOf(torrentSectionEnd, posTorSectionStart + 1);

                // find all the torrents
                int posTorDownloadStart = output.IndexOf(torDownloadStart, posTorSectionStart + 1);

                while (posTorDownloadStart < posTorSectionEnd && posTorDownloadStart > 0)
                {
                    int posTorDownloadEnd = output.IndexOf(torDownloadEnd, posTorDownloadStart + 9);

                    string torDownloadLink = output.Substring(posTorDownloadStart + 9, posTorDownloadEnd - posTorDownloadStart - 9);
                    torDownloadLink = HttpUtility.HtmlDecode(torDownloadLink);

                    int posTorInfoPreStart = output.IndexOf(torInfoPreStart, posTorDownloadEnd + 1);
                    int posTorInfoStart = output.IndexOf(torInfoStart, posTorInfoPreStart + 1);
                    int posTorInfoEnd = output.IndexOf(torInfoEnd, posTorInfoStart + 1);

                    string torInfo = output.Substring(posTorInfoStart + 1, posTorInfoEnd - posTorInfoStart - 1);

                    // TODO - extract out <img> imfomation
                    // <img src="static/common/hentaic.png" alt="Hentai" title="This torrent is of censored hentai (18+) material!" />
                    // <img src="static/common/flicon.png" alt="Freeleech!" title="This torrent is freeleech. Remember to seed!" />
                    int posImgStart = torInfo.IndexOf("<img src=", 0);
                    if (posImgStart >= 0)
                    {
                        bool hentai = torInfo.ToUpper().Contains(hentaiTag.ToUpper());
                        bool freeLeech = torInfo.ToUpper().Contains(leechTag.ToUpper());

                        // remove the img alts
                        torInfo = torInfo.Substring(0, posImgStart - 2);

                        if (hentai) torInfo = torInfo + " [hentai]";
                        if (freeLeech) torInfo = torInfo + " [FREE Leech]";
                    }


                    int posTorSizeStart = output.IndexOf(torSizeStart, posTorInfoEnd + 1);
                    int posTorSizeEnd = output.IndexOf(torSizeEnd, posTorSizeStart + 1);

                    string torSize = output.Substring(posTorSizeStart + torSizeStart.Length, posTorSizeEnd - posTorSizeStart - torSizeStart.Length);


                    int posTorSeedStart = output.IndexOf(torSeedStart, posTorSizeEnd + 1);
                    int posTorSeedEnd = output.IndexOf(torSeedEnd, posTorSeedStart + 1);

                    string torSeed = output.Substring(posTorSeedStart + torSeedStart.Length, posTorSeedEnd - posTorSeedStart - torSeedStart.Length);

                    int posTorLeechStart = output.IndexOf(torLeechStart, posTorSeedEnd + 1);
                    int posTorLeechEnd = output.IndexOf(torLeechEnd, posTorLeechStart + 1);

                    string torLeech = output.Substring(posTorLeechStart + torLeechStart.Length, posTorLeechEnd - posTorLeechStart - torLeechStart.Length);

                    TorrentLink torrentLink = new TorrentLink(TorrentSourceType.AnimeBytes)
                    {
                        TorrentDownloadLink = $@"http://animebytes.tv/{torDownloadLink}",
                        TorrentInfoLink = "",
                        AnimeType = torType,
                        TorrentName = torName + " - " + torInfo,
                        Size = torSize.Trim()
                    };
                    var strSeeders = torSeed.Trim();

                    if (double.TryParse(strSeeders, out double dblSeeders))
                        torrentLink.Seeders = dblSeeders;
                    else
                        torrentLink.Seeders = double.NaN;

                    var strLeechers = torLeech.Trim();

                    if (double.TryParse(strLeechers, out double dblLeechers))
                        torrentLink.Leechers = dblLeechers;
                    else
                        torrentLink.Leechers = double.NaN;

                    torLinks.Add(torrentLink);

                    // find the next download link
                    posTorDownloadStart = output.IndexOf(torDownloadStart, posTorLeechEnd + 1);
                }

                // find the next torrent group
                pos = output.IndexOf(nameStart, pos + 3);
            }

            return torLinks;
        }

        public List<TorrentLink> GetTorrents(List<string> searchParms)
        {
            try
            {
                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesUsername) || string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesPassword))
                    return new List<TorrentLink>();

                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesCookieHeader))
                {
                    string cookie = Login(TorrentSettings.Instance.AnimeBytesUsername, TorrentSettings.Instance.AnimeBytesPassword);
                    TorrentSettings.Instance.AnimeBytesCookieHeader = cookie;
                }

                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesCookieHeader))
                    return new List<TorrentLink>();

                string urlBase = "http://animebytes.tv/torrents.php?filter_cat%5B1%5D=1&searchstr={0}&action=advanced&search_type=title&year=&year2=&tags=&tags_type=0&sort=time_added&way=desc&hentai=2&releasegroup=&epcount=&epcount2=&artbooktitle=";

                string searchCriteria = "";
                foreach (string parm in searchParms)
                {
                    if (searchCriteria.Length > 0) searchCriteria += "+";
                    searchCriteria += parm.Trim();
                }

                string url = string.Format(urlBase, searchCriteria);
                string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, TorrentSettings.Instance.AnimeBytesCookieHeader, true);

                return ParseSource(output);
            }
            catch
            {
                return new List<TorrentLink>();
            }
        }

        public List<TorrentLink> BrowseTorrents()
        {
            try
            {
                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesUsername) || string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesPassword))
                    return new List<TorrentLink>();

                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesCookieHeader))
                {
                    string cookie = Login(TorrentSettings.Instance.AnimeBytesUsername, TorrentSettings.Instance.AnimeBytesPassword);
                    TorrentSettings.Instance.AnimeBytesCookieHeader = cookie;
                }

                if (string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesCookieHeader))
                    return new List<TorrentLink>();

                string url = "http://animebytes.tv/torrents.php?filter_cat[1]=1&sort=time_added&way=desc";
                string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, TorrentSettings.Instance.AnimeBytesCookieHeader, true);

                return ParseSource(output);
            }
            catch
            {
                return new List<TorrentLink>();
            }

            /*string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

			string fileName = "source.txt";
			string fileNameWithPath = Path.Combine(appPath, fileName);

			StreamReader re = File.OpenText(fileNameWithPath);
			string rawText = re.ReadToEnd();
			re.Close();


			return ParseSource(rawText);*/
        }

        private List<Cookie> GetAllCookies(CookieContainer cc)
        {
            List<Cookie> lstCookies = new List<Cookie>();

            Hashtable table = (Hashtable)cc.GetType().InvokeMember("m_domainTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance, null, cc, new object[] { });

            foreach (var pathList in table.Values)
            {
                SortedList lstCookieCol = (SortedList)pathList.GetType().InvokeMember("m_list", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance, null, pathList, new object[] { });
                foreach (CookieCollection colCookies in lstCookieCol.Values)
                    foreach (Cookie c in colCookies) lstCookies.Add(c);
            }

            return lstCookies;
        }

        private string ShowAllCookies(CookieContainer cc)
        {
            StringBuilder sb = new StringBuilder();
            List<Cookie> lstCookies = GetAllCookies(cc);
            sb.AppendLine("=========================================================== ");
            sb.AppendLine(lstCookies.Count + " cookies found.");
            sb.AppendLine("=========================================================== ");
            int cpt = 1;
            foreach (Cookie c in lstCookies)
                sb.AppendLine("#" + cpt++ + "> Name: " + c.Name + "\tValue: " + c.Value + "\tDomain: " + c.Domain + "\tPath: " + c.Path + "\tExp: " + c.Expires.ToString());

            return sb.ToString();
        }

        #endregion
    }
}
