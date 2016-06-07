using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerKodi
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{UserId}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        Stream GetFilters(string UserId);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{UserId}/{TypeId}/{Id}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        Stream GetMetadata(string UserId, string TypeId, string Id);

        [OperationContract]
        [WebGet(UriTemplate = "GetFile/{Id}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetFile(string Id);

        [OperationContract]
        [WebGet(UriTemplate = "GetUsers", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetUsers();

        [OperationContract]
        [WebGet(UriTemplate = "Search/{UserId}/{limit}/{query}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Stream Search(string UserId, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "SearchTag/{UserId}/{limit}/{query}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Stream SearchTag(string UserId, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Stream GetSupportImage(string name);

        [OperationContract]
        [WebGet(UriTemplate = "Watch/{userid}/{episodeid}/{watchedstatus}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        void ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus);

        [OperationContract]
        [WebGet(UriTemplate = "Vote/{userid}/{seriesid}/{votevalue}/{votetype}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Stream VoteAnime(string userid, string seriesid, string votevalue, string votetype);

        [OperationContract]
        [WebGet(UriTemplate = "TraktScrobble/{animeId}/{type}/{progress}/{status}",
            ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        Stream TraktScrobble(string animeId, string type, string progress, string status);
    }
}