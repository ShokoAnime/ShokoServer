using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Enums;

namespace Shoko.Commons.Downloads
{
    public class TorrentSettings:  ITorrentSettings
    {

        private Func<Expression<Func<object>>, object> _getFunction;
        private Action<Expression<Func<object>>, object> _setFunction;
        private Func<TorrentSourceType, bool, TorrentSource> _source_creator_function;

        public void SetGetAndSetCallback(Func<Expression<Func<object>>,object> getFunction, Action<Expression<Func<object>>, object> setFunction, Func<TorrentSourceType, bool, TorrentSource> source_creator_function)
        {
            _getFunction = getFunction;
            _setFunction = setFunction;
            _source_creator_function = source_creator_function;
        }

        public TorrentSource Create(TorrentSourceType tp, bool enabled)
        {
            return _source_creator_function?.Invoke(tp, enabled);
        }

        public static TorrentSettings Instance { get; } = new TorrentSettings();

        public bool TorrentBlackhole
        {
            get
            {
                return (bool) _getFunction(() => TorrentBlackhole);
            }
            set
            {
                _setFunction(() => TorrentBlackhole, value);
            }
        }

        public string TorrentBlackholeFolder
        {
            get
            {
                return (string)_getFunction(() => TorrentBlackholeFolder);
            }
            set
            {
                _setFunction(() => TorrentBlackholeFolder, value);
            }
        }

        public string UTorrentAddress
        {
            get
            {
                return (string)_getFunction(() => UTorrentAddress);
            }
            set
            {
                _setFunction(() => UTorrentAddress, value);
            }
        }



        public string UTorrentPort
        {
            get
            {
                return (string)_getFunction(() => UTorrentPort);
            }
            set
            {
                _setFunction(() => UTorrentPort, value);
            }
        }

        public string UTorrentUsername
        {
            get
            {
                return (string)_getFunction(() => UTorrentUsername);
            }
            set
            {
                _setFunction(() => UTorrentUsername, value);
            }
        }
        public string UTorrentPassword
        {
            get
            {
                return (string)_getFunction(() => UTorrentPassword);
            }
            set
            {
                _setFunction(() => UTorrentPassword, value);
            }
        }

        public int UTorrentRefreshInterval
        {
            get
            {
                return (int)_getFunction(() => UTorrentRefreshInterval);
            }
            set
            {
                _setFunction(() => UTorrentRefreshInterval, value);
            }
        }
        public bool UTorrentAutoRefresh
        {
            get
            {
                return (bool)_getFunction(() => UTorrentAutoRefresh);
            }
            set
            {
                _setFunction(() => UTorrentAutoRefresh, value);
            }
        }

        public bool TorrentSearchPreferOwnGroups
        {
            get
            {
                return (bool)_getFunction(() => TorrentSearchPreferOwnGroups);
            }
            set
            {
                _setFunction(() => TorrentSearchPreferOwnGroups, value);
            }
        }

        public string BakaBTUsername
        {
            get
            {
                return (string)_getFunction(() => BakaBTUsername);
            }
            set
            {
                _setFunction(() => BakaBTUsername, value);
            }
        }

        public string BakaBTPassword
        {
            get
            {
                return (string)_getFunction(() => BakaBTPassword);
            }
            set
            {
                _setFunction(() => BakaBTPassword, value);
            }
        }

        public bool BakaBTOnlyUseForSeriesSearches
        {
            get
            {
                return (bool)_getFunction(() => BakaBTOnlyUseForSeriesSearches);
            }
            set
            {
                _setFunction(() => BakaBTOnlyUseForSeriesSearches, value);
            }
        }

        public string BakaBTCookieHeader
        {
            get
            {
                return (string)_getFunction(() => BakaBTCookieHeader);
            }
            set
            {
                _setFunction(() => BakaBTCookieHeader, value);
            }
        }

        public string AnimeBytesUsername
        {
            get
            {
                return (string)_getFunction(() => AnimeBytesUsername);
            }
            set
            {
                _setFunction(() => AnimeBytesUsername, value);
            }
        }

        public string AnimeBytesPassword
        {
            get
            {
                return (string)_getFunction(() => AnimeBytesPassword);
            }
            set
            {
                _setFunction(() => AnimeBytesPassword, value);
            }
        }

        public bool AnimeBytesOnlyUseForSeriesSearches
        {
            get
            {
                return (bool)_getFunction(() => AnimeBytesOnlyUseForSeriesSearches);
            }
            set
            {
                _setFunction(() => AnimeBytesOnlyUseForSeriesSearches, value);
            }
        }
        public string AnimeBytesCookieHeader
        {
            get
            {
                return (string)_getFunction(() => AnimeBytesCookieHeader);
            }
            set
            {
                _setFunction(() => AnimeBytesCookieHeader, value);
            }
        }
        public ObservableCollection<TorrentSource> UnselectedTorrentSources
        {
            get
            {
                return (ObservableCollection<TorrentSource>)_getFunction(() => UnselectedTorrentSources);
            }
            set
            {
                _setFunction(() => UnselectedTorrentSources, value);
            }
        }
        public ObservableCollection<TorrentSource> SelectedTorrentSources
        {
            get
            {
                return (ObservableCollection<TorrentSource>)_getFunction(() => SelectedTorrentSources);
            }
            set
            {
                _setFunction(() => SelectedTorrentSources, value);
            }
        }
        public ObservableCollection<TorrentSource> AllTorrentSources
        {
            get
            {
                return (ObservableCollection<TorrentSource>)_getFunction(() => AllTorrentSources);
            }
            set
            {
                _setFunction(() => AllTorrentSources, value);
            }
        }
        public ObservableCollection<TorrentSource> CurrentSearchTorrentSources
        {
            get
            {
                return (ObservableCollection<TorrentSource>)_getFunction(() => CurrentSearchTorrentSources);
            }
            set
            {
                _setFunction(() => CurrentSearchTorrentSources, value);
            }
        }

    }
}
