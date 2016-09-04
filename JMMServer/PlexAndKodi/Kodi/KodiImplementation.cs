using JMMContracts;
using JMMContracts.PlexAndKodi;
using Stream = System.IO.Stream;

namespace JMMServer.PlexAndKodi.Kodi
{
    public class KodiImplementation : IJMMServerKodi
    {
        IProvider _prov = new KodiProvider();
        CommonImplementation _impl = new CommonImplementation();


        public MediaContainer GetFilters(string userid)
        {

            return _impl.GetFilters(_prov, userid);
        }

        public MediaContainer GetMetadata(string userid, string typeid, string id)
        {
            return _impl.GetMetadata(_prov, userid, typeid, id, null);
        }

		public MediaContainer GetMetadataNoCast(string userid, string typeid, string id)
		{
			return _impl.GetMetadata(_prov, userid, typeid, id, null, true);
		}

		public Response GetVersion()
        {
            return _impl.GetVersion(_prov);
        }

        public PlexContract_Users GetUsers()
        {
            return _impl.GetUsers(_prov);
        }

        public MediaContainer Search(string userid, string limit, string query)
        {
            return _impl.Search(_prov, userid, limit, query, false);
        }

        public MediaContainer SearchTag(string userid, string limit, string query)
        {
            return _impl.Search(_prov, userid, limit, query, true);
        }

        public Stream GetSupportImage(string name)
        {
            return _impl.GetSupportImage(name);
        }

        public Response ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus)
        {
            return _impl.ToggleWatchedStatusOnEpisode(_prov, userid, episodeid, watchedstatus);
        }

		public Response ToggleWatchedStatusOnSeries(string userid, string seriesid, string watchedstatus)
		{
			return _impl.ToggleWatchedStatusOnSeries(_prov, userid, seriesid, watchedstatus);
		}

		public Response ToggleWatchedStatusOnGroup(string userid, string groupid, string watchedstatus)
		{
			return _impl.ToggleWatchedStatusOnGroup(_prov, userid, groupid, watchedstatus);
		}

		public Response VoteAnime(string userid, string objectid, string votevalue, string votetype)
        {
            return _impl.VoteAnime(_prov, userid, objectid, votevalue, votetype);
        }

        public Response TraktScrobble(string animeid, string type, string progress, string status)
        {
            return _impl.TraktScrobble(_prov, animeid, type, progress, status);
        }
    }
}