using System.IO;
using JMMContracts;

namespace JMMServer.PlexAndKodi.Kodi
{
    public class KodiImplementation : IJMMServerKodi
    {
        IProvider _prov = new KodiProvider();
        CommonImplementation _impl = new CommonImplementation();


        public Stream GetFilters(string userid)
        {
            return _impl.GetFilters(_prov, userid);
        }

        public Stream GetMetadata(string userid, string typeid, string id)
        {
            return _impl.GetMetadata(_prov, userid, typeid, id, null);
        }

        public Stream GetVersion()
        {
            return _impl.GetVersion(_prov);
        }

        public Stream GetUsers()
        {
            return _impl.GetUsers(_prov);
        }

        public Stream Search(string userid, string limit, string query)
        {
            return _impl.Search(_prov, userid, limit, query, false);
        }

        public Stream SearchTag(string userid, string limit, string query)
        {
            return _impl.Search(_prov, userid, limit, query, true);
        }

        public Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        public Stream ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus)
        {
            return _impl.ToggleWatchedStatusOnEpisode(_prov, userid, episodeid, watchedstatus);
        }

        public Stream VoteAnime(string userid, string objectid, string votevalue, string votetype)
        {
            return _impl.VoteAnime(_prov, userid, objectid, votevalue, votetype);
        }

        public Stream TraktScrobble(string animeid, string type, string progress, string status)
        {
            return _impl.TraktScrobble(_prov, animeid, type, progress, status);
        }
    }
}