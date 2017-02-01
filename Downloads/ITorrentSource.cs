using System.Collections.Generic;

namespace Shoko.Commons.Downloads
{
    public interface ITorrentSource
    {
        string GetSourceName();
        string GetSourceNameShort();
        List<TorrentLink> GetTorrents(List<string> searchParms);
        bool SupportsSearching();
        bool SupportsBrowsing();
        bool SupportsCRCMatching();
    }
}
