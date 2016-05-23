using JMMContracts;
using Stream = System.IO.Stream;

namespace JMMServer.PlexAndKodi.Plex
{
    public class PlexImplementation : IJMMServerPlex
    {
        IProvider _prov=new PlexProvider();
        CommonImplementation _impl=new CommonImplementation();
        
        public Stream GetFilters(string userid)
        {
            return _impl.GetFilters(_prov, userid);
        }

        public Stream GetMetadata(string userid, string typeid, string id, string hkey)
        {
            return _impl.GetMetadata(_prov, userid, typeid, id, hkey);
        }

        public Stream GetUsers()
        {
            return _impl.GetUsers(_prov);
        }

        public Stream Search(string userid, string limit, string query)
        {
            return _impl.Search(_prov, userid, limit, query, false);
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
    }
}
