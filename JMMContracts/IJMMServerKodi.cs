using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using JMMContracts.PlexAndKodi;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerKodi
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{userid}", BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        MediaContainer GetFilters(string userid);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{userid}/{typeid}/{id}", BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        MediaContainer GetMetadata(string userid, string typeid, string id);

        [OperationContract]
        [WebGet(UriTemplate = "GetUsers", BodyStyle = WebMessageBodyStyle.Bare)]
        PlexContract_Users GetUsers();

        [OperationContract]
        [WebGet(UriTemplate = "Search/{userid}/{limit}/{query}", BodyStyle = WebMessageBodyStyle.Bare)]
        MediaContainer Search(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "SearchTag/{userid}/{limit}/{query}", BodyStyle = WebMessageBodyStyle.Bare)]
        MediaContainer SearchTag(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}")]
        System.IO.Stream GetSupportImage(string name);

        [OperationContract]
        [WebGet(UriTemplate = "Watch/{userid}/{episodeid}/{watchedstatus}", BodyStyle = WebMessageBodyStyle.Bare)]
        Response ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus);

        [OperationContract]
        [WebGet(UriTemplate = "Vote/{userid}/{seriesid}/{votevalue}/{votetype}", BodyStyle = WebMessageBodyStyle.Bare)]
        Response VoteAnime(string userid, string seriesid, string votevalue, string votetype);

        [OperationContract]
        [WebGet(UriTemplate = "TraktScrobble/{animeid}/{type}/{progress}/{status}", BodyStyle = WebMessageBodyStyle.Bare)]
        Response TraktScrobble(string animeid, string type, string progress, string status);

        [OperationContract]
        [WebGet(UriTemplate = "GetVersion", BodyStyle = WebMessageBodyStyle.Bare)]
        Response GetVersion();
    }
}