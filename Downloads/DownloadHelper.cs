using System;
using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class DownloadHelper
    {

        public static string FixNyaaTorrentLink(string url)
        {
            // on some trackers the user will post the torrent page instead of the 
            // direct torrent link
            return url.Replace("page=torrentinfo", "page=download");
        }

        public static List<TorrentLink> SearchTorrents(DownloadSearchCriteria search)
        {
            List<string> parms = search.SearchParameter;
            List<TorrentLink> links = new List<TorrentLink>();

            List<string> episodeGroupParms = new List<string>();

            // get the sources that are in both the selected sources and the default sources
            // default sources have an order
            List<TorrentSource> orderedSources = new List<TorrentSource>();

            // if only full torrent sites
            bool onlyFullSites = false;
            if (search.SearchType == DownloadSearchType.Series)
            {
                if (TorrentSettings.Instance.BakaBTOnlyUseForSeriesSearches &&
                !string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTUsername) && !string.IsNullOrEmpty(TorrentSettings.Instance.BakaBTPassword))
                {
                    onlyFullSites = true;
                    TorrentSource src = TorrentSettings.Instance.Create(TorrentSourceType.BakaBT, true);
                    orderedSources.Add(src);
                }

                if (TorrentSettings.Instance.AnimeBytesOnlyUseForSeriesSearches &&
                !string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesUsername) && !string.IsNullOrEmpty(TorrentSettings.Instance.AnimeBytesPassword))
                {
                    onlyFullSites = true;
                    TorrentSource src = TorrentSettings.Instance.Create(TorrentSourceType.AnimeBytes, true);
                    orderedSources.Add(src);
                }
            }

            if (!onlyFullSites)
            {
                foreach (TorrentSource src in TorrentSettings.Instance.SelectedTorrentSources)
                {
                    foreach (TorrentSource srcCur in TorrentSettings.Instance.CurrentSearchTorrentSources)
                    {
                        if (!srcCur.IsEnabled) continue;
                        if (src.TorrentSourceType == srcCur.TorrentSourceType)
                            orderedSources.Add(srcCur);
                    }
                }

                // now get any sources that we missed
                foreach (TorrentSource src in TorrentSettings.Instance.CurrentSearchTorrentSources)
                {
                    if (!src.IsEnabled) continue;
                    bool foundSource = false;
                    foreach (TorrentSource srcDone in orderedSources)
                    {
                        if (srcDone.TorrentSourceType == src.TorrentSourceType) foundSource = true;
                    }
                    if (!foundSource)
                        orderedSources.Add(src);
                }
            }

            foreach (TorrentSource src in orderedSources)
            {
                if (!src.IsEnabled) continue;

                if (src.TorrentSourceType == TorrentSourceType.Nyaa)
                {
                    TorrentsNyaa nyaa = new TorrentsNyaa();
                    List<TorrentLink> ttLinks = null;
                    Dictionary<string, TorrentLink> dictLinks = new Dictionary<string, TorrentLink>();

                    foreach (string grp in episodeGroupParms)
                    {
                        List<string> tempParms = new List<string>();
                        foreach (string parmTemp in parms)
                            tempParms.Add(parmTemp);
                        tempParms.Insert(0, grp);
                        ttLinks = nyaa.GetTorrents(tempParms);


                        // only use the first 10
                        int x = 0;
                        foreach (TorrentLink link in ttLinks)
                        {
                            if (x == 10) break;
                            dictLinks[link.TorrentDownloadLink] = link;
                        }
                    }

                    ttLinks = nyaa.GetTorrents(parms);
                    foreach (TorrentLink link in ttLinks)
                    {
                        dictLinks[link.TorrentDownloadLink] = link;
                        //logger.Trace("Adding link: " + link.ToString());
                    }

                    links.AddRange(dictLinks.Values);
                }

                if (src.TorrentSourceType == TorrentSourceType.Sukebei)
                {
                    TorrentsSukebei sukebei = new TorrentsSukebei();
                    List<TorrentLink> ttLinks = null;
                    Dictionary<string, TorrentLink> dictLinks = new Dictionary<string, TorrentLink>();

                    foreach (string grp in episodeGroupParms)
                    {
                        List<string> tempParms = new List<string>();
                        foreach (string parmTemp in parms)
                            tempParms.Add(parmTemp);
                        tempParms.Insert(0, grp);
                        ttLinks = sukebei.GetTorrents(tempParms);


                        // only use the first 10
                        int x = 0;
                        foreach (TorrentLink link in ttLinks)
                        {
                            if (x == 10) break;
                            dictLinks[link.TorrentDownloadLink] = link;
                        }
                    }

                    ttLinks = sukebei.GetTorrents(parms);
                    foreach (TorrentLink link in ttLinks)
                    {
                        dictLinks[link.TorrentDownloadLink] = link;
                        //logger.Trace("Adding link: " + link.ToString());
                    }

                    links.AddRange(dictLinks.Values);
                }

                if (src.TorrentSourceType == TorrentSourceType.BakaBT)
                {
                    TorrentsBakaBT bakaBT = new TorrentsBakaBT();
                    List<TorrentLink> bbLinks = bakaBT.GetTorrents(parms);
                    links.AddRange(bbLinks);
                }

                if (src.TorrentSourceType == TorrentSourceType.AnimeBytes)
                {
                    TorrentsAnimeBytes abytes = new TorrentsAnimeBytes();
                    List<TorrentLink> abytesLinks = abytes.GetTorrents(parms);
                    links.AddRange(abytesLinks);
                }

                if (src.TorrentSourceType == TorrentSourceType.TokyoToshokanAll || src.TorrentSourceType == TorrentSourceType.TokyoToshokanAnime)
                {
                    TorrentsTokyoToshokan tt = new TorrentsTokyoToshokan(src.TorrentSourceType);
                    List<TorrentLink> ttLinks = null;
                    Dictionary<string, TorrentLink> dictLinks = new Dictionary<string, TorrentLink>();

                    foreach (string grp in episodeGroupParms)
                    {
                        List<string> tempParms = new List<string>();
                        foreach (string parmTemp in parms)
                            tempParms.Add(parmTemp);
                        tempParms.Insert(0, grp);
                        ttLinks = tt.GetTorrents(tempParms);


                        // only use the first 10
                        int x = 0;
                        foreach (TorrentLink link in ttLinks)
                        {
                            if (x == 0) break;
                            dictLinks[link.TorrentDownloadLink] = link;
                            //logger.Trace("Adding link: " + link.ToString());
                        }
                    }

                    ttLinks = tt.GetTorrents(parms);
                    foreach (TorrentLink link in ttLinks)
                    {
                        dictLinks[link.TorrentDownloadLink] = link;
                        //logger.Trace("Adding link: " + link.ToString());
                    }

                    links.AddRange(dictLinks.Values);
                }


            }



            return links;
        }
    }
}
