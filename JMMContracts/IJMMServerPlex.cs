using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerPlex
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{UserId}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        Stream GetFilters(string UserId);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{UserId}/{TypeId}/{Id}/{hkey}", ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        Stream GetMetadata(string UserId, string TypeId, string Id, string hkey);

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
        void VoteAnime(string userid, string seriesid, string votevalue, string votetype);
    }
}