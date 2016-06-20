using System.ServiceModel;
using System.ServiceModel.Web;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerKodi
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{userid}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        System.IO.Stream GetFilters(string userid);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{userid}/{typeid}/{id}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        System.IO.Stream GetMetadata(string userid, string typeid, string id);

        [OperationContract]
        [WebGet(UriTemplate = "GetUsers", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetUsers();

        [OperationContract]
        [WebGet(UriTemplate = "Search/{userid}/{limit}/{query}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream Search(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "SearchTag/{userid}/{limit}/{query}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream SearchTag(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetSupportImage(string name);

        [OperationContract]
        [WebGet(UriTemplate = "Watch/{userid}/{episodeid}/{watchedstatus}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus);

        [OperationContract]
        [WebGet(UriTemplate = "Vote/{userid}/{seriesid}/{votevalue}/{votetype}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream VoteAnime(string userid, string seriesid, string votevalue, string votetype);

        [OperationContract]
        [WebGet(UriTemplate = "TraktScrobble/{animeid}/{type}/{progress}/{status}",
            ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream TraktScrobble(string animeid, string type, string progress, string status);

        [OperationContract]
        [WebGet(UriTemplate = "GetVersion", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetVersion();
    }
}