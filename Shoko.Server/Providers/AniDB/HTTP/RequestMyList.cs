using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestMyList : HttpBaseRequest<List<ResponseMyList>>
    {

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={Username}&pass={Password}";
        
        public string Username { private get; init; }
        public string Password { private get; init; }
        
        protected override HttpBaseResponse<List<ResponseMyList>> ParseResponse(ILogger logger, HttpBaseResponse<string> data)
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
                        var updated = (DateTime?) null;
                        if (DateTime.TryParse(item.Attribute("updated")?.Value, out var tempu)) updated = tempu;
                        var viewed = (DateTime?) null;
                        if (DateTime.TryParse(item.Attribute("viewdate")?.Value, out var tempv)) viewed = tempv;
                        var stateI = (int?) item.Element("state");
                        var state = stateI.HasValue ? (MyList_State) stateI.Value : MyList_State.Unknown;
                        return new ResponseMyList
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
                return new HttpBaseResponse<List<ResponseMyList>> {Code = data.Code, Response = responses};
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return new HttpBaseResponse<List<ResponseMyList>> {Code = data.Code, Response = null};
            }
        }
    }
}
