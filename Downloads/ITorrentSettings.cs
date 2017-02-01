using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Commons.Downloads
{
    public interface ITorrentSettings
    {
        bool TorrentBlackhole { get; set; }
 
        string TorrentBlackholeFolder { get; set; }

        string UTorrentAddress { get; set; }

        string UTorrentPort { get; set; }

        string UTorrentUsername { get; set; }

        string UTorrentPassword { get; set; }
        int UTorrentRefreshInterval { get; set; }

        bool UTorrentAutoRefresh { get; set; }
        bool TorrentSearchPreferOwnGroups { get; set; }
        string BakaBTUsername { get; set; }
        string BakaBTPassword { get; set; }
        bool BakaBTOnlyUseForSeriesSearches { get; set; }
        string BakaBTCookieHeader { get; set; }
        string AnimeBytesUsername { get; set; }
        string AnimeBytesPassword { get; set; }
        bool AnimeBytesOnlyUseForSeriesSearches { get; set; }
        string AnimeBytesCookieHeader { get; set; }

        ObservableCollection<TorrentSource> UnselectedTorrentSources { get; set; }
        ObservableCollection<TorrentSource> SelectedTorrentSources { get; set; }
        ObservableCollection<TorrentSource> AllTorrentSources { get; set; }
        ObservableCollection<TorrentSource> CurrentSearchTorrentSources { get; set; }
    }
}
