using System.Collections.Generic;
using Shoko.Commons.Languages;
using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class TorrentSource
    {
        public TorrentSourceType TorrentSourceType { get; set; }

        public bool IsEnabled { get; set; }

        public string TorrentSourceName => EnumTranslator.TorrentSourceTranslated(TorrentSourceType);

        public string TorrentSourceNameShort => EnumTranslator.TorrentSourceTranslatedShort(TorrentSourceType);

        public TorrentSource(TorrentSourceType tsType, bool isEnabled)
        {
            TorrentSourceType = tsType;
            IsEnabled = isEnabled;
        }

        public void PopulateTorrentDownloadLink(ref TorrentLink torLink)
        {
            if (TorrentSourceType == TorrentSourceType.BakaBT)
            {
                if (string.IsNullOrEmpty(torLink.TorrentDownloadLink))
                {
                    TorrentsBakaBT bakbt = new TorrentsBakaBT();
                    bakbt.PopulateTorrentLink(ref torLink);
                }
            }
        }

        public List<TorrentLink> BrowseTorrents()
        {
            List<TorrentLink> links = new List<TorrentLink>();

            if (TorrentSourceType == TorrentSourceType.Nyaa)
            {
                TorrentsNyaa nyaa = new TorrentsNyaa();
                List<TorrentLink> ttLinks = nyaa.BrowseTorrents();
                links.AddRange(ttLinks);
            }

            if (TorrentSourceType == TorrentSourceType.Sukebei)
            {
                TorrentsSukebei sukebei = new TorrentsSukebei();
                List<TorrentLink> ttLinks = sukebei.BrowseTorrents();
                links.AddRange(ttLinks);
            }

            if (TorrentSourceType == TorrentSourceType.TokyoToshokanAnime)
            {
                TorrentsTokyoToshokan tt = new TorrentsTokyoToshokan(TorrentSourceType.TokyoToshokanAnime);
                List<TorrentLink> ttLinks = tt.BrowseTorrents();
                links.AddRange(ttLinks);
            }

            if (TorrentSourceType == TorrentSourceType.TokyoToshokanAll)
            {
                TorrentsTokyoToshokan tt = new TorrentsTokyoToshokan(TorrentSourceType.TokyoToshokanAll);
                List<TorrentLink> ttLinks = tt.BrowseTorrents();
                links.AddRange(ttLinks);
            }

            if (TorrentSourceType == TorrentSourceType.BakaBT)
            {
                TorrentsBakaBT bakbt = new TorrentsBakaBT();
                List<TorrentLink> bakauLinks = bakbt.BrowseTorrents();
                links.AddRange(bakauLinks);
            }

            if (TorrentSourceType == TorrentSourceType.AnimeBytes)
            {
                TorrentsAnimeBytes abytes = new TorrentsAnimeBytes();
                List<TorrentLink> abytesLinks = abytes.BrowseTorrents();
                links.AddRange(abytesLinks);
            }


            return links;
        }
        
    }
}
