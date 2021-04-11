using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestMyList : HttpBaseRequest<List<ResponseMyList>>
    {
        public Logger Logger = LogManager.GetCurrentClassLogger();

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={Username}&pass={Password}";
        
        private string Username { get; set; }
        private string Password { get; set; }
        
        protected override HttpBaseResponse<List<ResponseMyList>> ParseResponse(HttpBaseResponse<string> data)
        {
            try
            {
                var doc = XDocument.Parse(data.Response);
                var mylist = doc.Descendants("mylist");
                if (mylist == null) throw new UnexpectedHttpResponseException("mylist tag not found", data.Code, data.Response);
                var items = mylist.Descendants("mylistitem");
                var responses = items.Select(
                    item =>
                    {
                        var id = (int?) item.Attribute("id");
                        var aid = (int?) item.Attribute("aid");
                        var eid = (int?) item.Attribute("eid");
                        var fid = (int?) item.Attribute("fid");
                        DateTime? updated = (DateTime?) null;
                        if (DateTime.TryParse(item.Attribute("updated")?.Value, out DateTime tempu)) updated = tempu;
                        DateTime? viewed = (DateTime?) null;
                        if (DateTime.TryParse(item.Attribute("viewdate")?.Value, out DateTime tempv)) viewed = tempv;
                        int? stateI = (int?) item.Element("state");
                        var state = stateI.HasValue ? (MyList_State) stateI.Value : MyList_State.Unknown;
                        return new ResponseMyList()
                        {
                            MyListID = id,
                            AnimeID = aid,
                            EpisodeID = eid,
                            FileID = fid,
                            UpdatedAt = updated,
                            ViewedAt = viewed,
                            State = state,
                        };
                    }
                ).ToList();
                return new HttpBaseResponse<List<ResponseMyList>>() {Code = data.Code, Response = responses};
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                return new HttpBaseResponse<List<ResponseMyList>>() {Code = data.Code, Response = null};
            }
        }
    }
}
