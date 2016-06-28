using System.ServiceModel;
using System.ServiceModel.Web;
using JMMContracts.PlexAndKodi;

namespace JMMContracts
{
    [ServiceContract]
    [XmlSerializerFormat]

    public interface IJMMServerPlex
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{userid}", BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        MediaContainer GetFilters(string userid);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{userid}/{typeid}/{id}/{hkey}", BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        MediaContainer GetMetadata(string userid, string typeid, string id, string hkey);

        [OperationContract]
        [WebGet(UriTemplate = "GetUsers", BodyStyle = WebMessageBodyStyle.Bare)]
        PlexContract_Users GetUsers();

        [OperationContract]
        [WebGet(UriTemplate = "Search/{userid}/{limit}/{query}", BodyStyle = WebMessageBodyStyle.Bare)]
        MediaContainer Search(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}", BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetSupportImage(string name);

        [OperationContract]
        [WebGet(UriTemplate = "Watch/{userid}/{episodeid}/{watchedstatus}", BodyStyle = WebMessageBodyStyle.Bare)]
        Response ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus);

        [OperationContract]
        [WebGet(UriTemplate = "Vote/{userid}/{objectid}/{votevalue}/{votetype}", BodyStyle = WebMessageBodyStyle.Bare)]
        Response VoteAnime(string userid, string objectid, string votevalue, string votetype);
    }
}