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
    public class TorrentsBakaBT : ITorrentSource
    {
        private TorrentSourceType SourceType = TorrentSourceType.BakaBT;

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
                using (var client = new WebClientEx())
                {
                    var values = new NameValueCollection
                    {
                        { "username", username },
                        { "password", password },
                    };
                    // Authenticate
                    client.UploadValues("https://bakabt.me/login.php", values);
                    if (ValidateCookie(client.CookieContainer.GetCookieHeader(new Uri("https://bakabt.me"))))
                        return client.CookieContainer.GetCookieHeader(new Uri("https://bakabt.me"));
                    else
                        return "";
                }

            }
            catch (Exception ex)
            {
                return "";
            }
        }

        private bool ValidateCookie(string cookie)
        {
            string urlBase = "https://bakabt.me/browse.php?only=0&hentai=1&incomplete=1&lossless=1&hd=1&multiaudio=1&bonus=1&c1=1&c2=1&c5=1&reorder=1&q={0}";

            string searchCriteria = "saint";

            string url = string.Format(urlBase, searchCriteria);
            string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, cookie, true);

            return ParseSource(output).Count > 1;
        }

        internal int count = 0;
        private List<TorrentLink> ParseSource(string output)
        {
            List<TorrentLink> torLinks = new List<TorrentLink>();

            char q = (char)34;
            string quote = q.ToString();

            //<td class="name">

            // remove html codes
            string rubbish1 = "<span class=" + quote + "highlight" + quote + ">";
            string rubbish2 = "</span>";



            //string startBlock = "<td class=" + quote + "name" + quote;
            string startBlock = "<td class=" + quote + "category";
            string altBlock = "class=" + quote + "alt_title" + quote;

            string catStart = "title=" + quote;
            string catEnd = quote;

            string linkStart = "href=" + quote;
            string linkEnd = quote;

            string nameStart = "title=" + quote + "Download torrent:";
            string nameStart2 = quote + ">";
            string nameEnd = "</a>";

            string sizeStart = "<td class=" + quote + "size" + quote + ">";
            string sizeEnd = "</td>";

            string seedInit = "<td class=" + quote + "peers" + quote + ">";
            string seedStart = quote + ">";
            string seedEnd = "</a>";

            string leechStart = quote + ">";
            string leechEnd = "</a>";

            int pos = output.IndexOf(startBlock, 0);
            while (pos > 0)
            {

                if (pos <= 0) break;

                int poscatStart = output.IndexOf(catStart, pos + 1);
                int poscatEnd = output.IndexOf(catEnd, poscatStart + catStart.Length + 1);

                string cat = output.Substring(poscatStart + catStart.Length, poscatEnd - poscatStart - catStart.Length);

                int poslinkStart = output.IndexOf(linkStart, poscatEnd + 1);
                int poslinkEnd = output.IndexOf(linkEnd, poslinkStart + linkStart.Length + 1);

                string link = output.Substring(poslinkStart + linkStart.Length, poslinkEnd - poslinkStart - linkStart.Length);

                int posnameStart = output.IndexOf(nameStart, poslinkEnd);
                int posnameStart2 = output.IndexOf(nameStart2, posnameStart + nameStart.Length);
                int posnameEnd = output.IndexOf(nameEnd, posnameStart2 + nameStart2.Length + 1);

                string torName = output.Substring(posnameStart2 + nameStart2.Length, posnameEnd - posnameStart2 - nameStart2.Length);

                torName = torName.Replace(rubbish1, "");
                torName = torName.Replace(rubbish2, "");

                // remove html codes
                torName = HttpUtility.HtmlDecode(torName);

                //Console.WriteLine("{0} - {1}", posNameStart, posNameEnd);

                string torSize = "";
                int posSizeStart = output.IndexOf(sizeStart, posnameEnd);
                int posSizeEnd = 0;
                if (posSizeStart > 0)
                {
                    posSizeEnd = output.IndexOf(sizeEnd, posSizeStart + sizeStart.Length + 1);

                    torSize = output.Substring(posSizeStart + sizeStart.Length, posSizeEnd - posSizeStart - sizeStart.Length);
                }

                int posSeedInit = output.IndexOf(seedInit, posSizeEnd);

                string torSeed = "";
                int posSeedStart = output.IndexOf(seedStart, posSeedInit + seedInit.Length + 1);
                int posSeedEnd = 0;
                if (posSeedStart > 0)
                {
                    posSeedEnd = output.IndexOf(seedEnd, posSeedStart + seedStart.Length + 1);

                    torSeed = output.Substring(posSeedStart + seedStart.Length, posSeedEnd - posSeedStart - seedStart.Length);
                }

                string torLeech = "";
                int posLeechStart = output.IndexOf(leechStart, posSeedStart + 3);
                int posLeechEnd = 0;
                if (posLeechStart > 0)
                {
                    posLeechEnd = output.IndexOf(leechEnd, posLeechStart + leechStart.Length + 1);

                    torLeech = output.Substring(posLeechStart + leechStart.Length, posLeechEnd - posLeechStart - leechStart.Length);
                }

                TorrentLink torrentLink = new TorrentLink(TorrentSourceType.BakaBT);
                torrentLink.TorrentDownloadLink = "";
                torrentLink.TorrentInfoLink = link;
                torrentLink.AnimeType = cat;
                torrentLink.TorrentName = $"[MAIN] {torName.Trim()}";
                torrentLink.Size = torSize.Trim();

                var strSeeders = torSeed.Trim();

                double dblSeeders;
                if (double.TryParse(strSeeders, out dblSeeders))
                    torrentLink.Seeders = dblSeeders;
                else
                    torrentLink.Seeders = double.NaN;

                var strLeechers = torLeech.Trim();

                double dblLeechers;
                if (double.TryParse(strLeechers, out dblLeechers))
                    torrentLink.Leechers = dblLeechers;
                else
                    torrentLink.Leechers = double.NaN;

                torLinks.Add(torrentLink);

                // now we have the main link provided by BakaBT
                // BakaBT also provides alternative links, so lets include those as well

                int temppos = output.IndexOf(startBlock, pos + 1);
                int altpos = output.IndexOf(altBlock, pos + 1);

                while (temppos > altpos && altpos > 0)
                {
                    string linkStartAlt = "href=" + quote;
                    string linkEndAlt = quote;

                    string nameStartAlt = quote + ">";
                    string nameEndAlt = "</a>";

                    string sizeStartAlt = "<td class=" + quote + "size" + quote + ">";
                    string sizeEndAlt = "</td>";

                    string seedInitAlt = "<td class=" + quote + "peers" + quote + ">";
                    string seedStartAlt = quote + ">";
                    string seedEndAlt = "</a>";

                    string leechStartAlt = quote + ">";
                    string leechEndAlt = "</a>";

                    int poslinkStartAlt = output.IndexOf(linkStartAlt, altpos + 1);
                    int poslinkEndAlt = output.IndexOf(linkEndAlt, poslinkStartAlt + linkStartAlt.Length + 1);

                    string linkAlt = output.Substring(poslinkStartAlt + linkStartAlt.Length, poslinkEndAlt - poslinkStartAlt - linkStartAlt.Length);

                    int posnameStartAlt = output.IndexOf(nameStartAlt, poslinkEndAlt);
                    int posnameEndAlt = output.IndexOf(nameEndAlt, posnameStartAlt + nameStartAlt.Length + 1);

                    string torNameAlt = output.Substring(posnameStartAlt + nameStartAlt.Length, posnameEndAlt - posnameStartAlt - nameStartAlt.Length);

                    // remove html codes
                    torNameAlt = torNameAlt.Replace(rubbish1, "");
                    torNameAlt = torNameAlt.Replace(rubbish2, "");

                    torNameAlt = HttpUtility.HtmlDecode(torNameAlt);

                    string torSizeAlt = "";
                    int posSizeStartAlt = output.IndexOf(sizeStartAlt, posnameEndAlt);
                    int posSizeEndAlt = 0;
                    if (posSizeStartAlt > 0)
                    {
                        posSizeEndAlt = output.IndexOf(sizeEndAlt, posSizeStartAlt + sizeStartAlt.Length + 1);

                        torSizeAlt = output.Substring(posSizeStartAlt + sizeStartAlt.Length, posSizeEndAlt - posSizeStartAlt - sizeStartAlt.Length);
                    }

                    int posSeedInitAlt = output.IndexOf(seedInitAlt, posSizeEndAlt);

                    string torSeedAlt = "";
                    int posSeedStartAlt = output.IndexOf(seedStartAlt, posSeedInitAlt + seedInitAlt.Length + 1);
                    int posSeedEndAlt = 0;
                    if (posSeedStartAlt > 0)
                    {
                        posSeedEndAlt = output.IndexOf(seedEndAlt, posSeedStartAlt + seedStartAlt.Length + 1);

                        torSeedAlt = output.Substring(posSeedStartAlt + seedStartAlt.Length, posSeedEndAlt - posSeedStartAlt - seedStartAlt.Length);
                    }

                    string torLeechAlt = "";
                    int posLeechStartAlt = output.IndexOf(leechStartAlt, posSeedStartAlt + 3);
                    int posLeechEndAlt = 0;
                    if (posLeechStartAlt > 0)
                    {
                        posLeechEndAlt = output.IndexOf(leechEndAlt, posLeechStartAlt + leechStartAlt.Length + 1);

                        torLeechAlt = output.Substring(posLeechStartAlt + leechStartAlt.Length, posLeechEndAlt - posLeechStartAlt - leechStartAlt.Length);
                    }

                    TorrentLink torrentLinkAlt = new TorrentLink(TorrentSourceType.BakaBT);
                    torrentLinkAlt.TorrentDownloadLink = "";
                    torrentLinkAlt.TorrentInfoLink = linkAlt;
                    torrentLinkAlt.AnimeType = cat;
                    torrentLinkAlt.TorrentName = $"[ALT] {torNameAlt.Trim()}";
                    torrentLinkAlt.Size = torSizeAlt.Trim();

                    var strSeedersAlt = torSeedAlt.Trim();

                    double dblSeedersAlt;
                    if (double.TryParse(strSeedersAlt, out dblSeedersAlt))
                        torrentLinkAlt.Seeders = dblSeedersAlt;
                    else
                        torrentLinkAlt.Seeders = double.NaN;

                    var strLeechersAlt = torLeechAlt.Trim();

                    double dblLeechersAlt;
                    if (double.TryParse(strLeechersAlt, out dblLeechersAlt))
                        torrentLinkAlt.Leechers = dblLeechersAlt;
                    else
                        torrentLinkAlt.Leechers = double.NaN;

                    torLinks.Add(torrentLinkAlt);

                    altpos = output.IndexOf(altBlock, posLeechEndAlt + 1);
                }

                pos = output.IndexOf(startBlock, pos + 1);



                //Console.WriteLine("{0} - {1}", torName, torLink);
            }
            //Console.ReadLine();

            count = torLinks.Count;
            return torLinks;
        }

        public List<TorrentLink> GetTorrents(List<string> searchParms)
        {
            try
            {
                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTUsername) || string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTPassword))
                    return new List<TorrentLink>();

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                {
                    string cookie = Login(TorrentSettings.Instance.BakaBTUsername, TorrentSettings.Instance.BakaBTPassword);
                    TorrentSettings.Instance.BakaBTCookieHeader = cookie;
                }

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                    return new List<TorrentLink>();

                string urlBase = "https://bakabt.me/browse.php?only=0&hentai=1&incomplete=1&lossless=1&hd=1&multiaudio=1&bonus=1&c1=1&c2=1&c5=1&reorder=1&q={0}";

                string searchCriteria = "";
                foreach (string parm in searchParms)
                {
                    if (searchCriteria.Length > 0) searchCriteria += "+";
                    searchCriteria += parm.Trim();
                }

                string url = string.Format(urlBase, searchCriteria);
                string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, TorrentSettings.Instance.BakaBTCookieHeader, true);

                return ParseSource(output);
            }
            catch (Exception ex)
            {
                return new List<TorrentLink>();
            }
        }

        public List<TorrentLink> BrowseTorrents()
        {
            try
            {
                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTUsername) || string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTPassword))
                    return new List<TorrentLink>();

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                {
                    string cookie = Login(TorrentSettings.Instance.BakaBTUsername, TorrentSettings.Instance.BakaBTPassword);
                    TorrentSettings.Instance.BakaBTCookieHeader = cookie;
                }

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                    return new List<TorrentLink>();

                string url = "https://bakabt.me/browse.php?only=0&hentai=1&incomplete=1&lossless=1&hd=1&multiaudio=1&bonus=1&c1=1&c2=1&c5=1&reorder=1&q=";
                string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, TorrentSettings.Instance.BakaBTCookieHeader, true);

                return ParseSource(output);
            }
            catch (Exception ex)
            {
                return new List<TorrentLink>();
            }

            /*string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			string fileName = "source.txt";
			string fileNameWithPath = Path.Combine(appPath, fileName);

			StreamReader re = File.OpenText(fileNameWithPath);
			string rawText = re.ReadToEnd();
			re.Close();


			return ParseSource(rawText);*/
        }

        public string GetTorrentLinkFromTorrentPage(string pageSource)
        {
            string startBlock = "<a href=\"download";

            string linkStart = "href=\"";
            string linkEnd = "\"";

            int pos = pageSource.IndexOf(startBlock, 0);

            if (pos <= 0) return null;

            int poslinkStart = pageSource.IndexOf(linkStart, pos + 1);
            int poslinkEnd = pageSource.IndexOf(linkEnd, poslinkStart + linkStart.Length + 1);

            string link = pageSource.Substring(poslinkStart + linkStart.Length, poslinkEnd - poslinkStart - linkStart.Length);

            return link;
        }

        public void PopulateTorrentLink(ref TorrentLink torLink)
        {
            try
            {
                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTUsername) || string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTPassword))
                    return;

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                {
                    string cookie = Login(TorrentSettings.Instance.BakaBTUsername, TorrentSettings.Instance.BakaBTPassword);
                    TorrentSettings.Instance.BakaBTCookieHeader = cookie;
                }

                if (string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTCookieHeader))
                    return;

                string url = torLink.TorrentLinkFull;
                string output = Shoko.Commons.Utils.Misc.DownloadWebPage(url, TorrentSettings.Instance.BakaBTCookieHeader, true);

                string torDownloadLink = GetTorrentLinkFromTorrentPage(output);
                torLink.TorrentDownloadLink = $"https://bakabt.me/{torDownloadLink}";
            }
            catch (Exception ex)
            {
                return;
            }
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
