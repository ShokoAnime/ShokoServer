using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using JMMContracts;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerPlex
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "GetFilters/{userid}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare,Method="*")]
        System.IO.Stream GetFilters(string userid);

        [OperationContract]
        [WebInvoke(UriTemplate = "GetMetadata/{userid}/{typeid}/{id}/{hkey}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare, Method = "*")]
        System.IO.Stream GetMetadata(string userid, string typeid, string id, string hkey);
       
        [OperationContract]
        [WebGet(UriTemplate = "GetUsers", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetUsers();

        [OperationContract]
        [WebGet(UriTemplate = "Search/{userid}/{limit}/{query}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream Search(string userid, string limit, string query);

        [OperationContract]
        [WebGet(UriTemplate = "GetSupportImage/{name}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream GetSupportImage(string name);

        [OperationContract]
        [WebGet(UriTemplate = "Watch/{userid}/{episodeid}/{watchedstatus}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus);

        [OperationContract]
        [WebGet(UriTemplate = "Vote/{userid}/{objectidid}/{votevalue}/{votetype}", ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
        System.IO.Stream VoteAnime(string userid, string objectid, string votevalue, string votetype);
    }
}
