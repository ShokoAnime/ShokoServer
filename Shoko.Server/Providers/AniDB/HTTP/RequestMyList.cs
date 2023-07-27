using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class RequestMyList : HttpRequest<List<ResponseMyList>>
{
    private readonly string _aniDBUrl;
    private readonly ushort _aniDBPort;

    protected override string BaseCommand =>
        $"http://{_aniDBUrl}:{_aniDBPort}/httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={Username}&pass={Password}";

    public string Username { private get; set; }
    public string Password { private get; set; }

    protected override HttpResponse<List<ResponseMyList>> ParseResponse(HttpResponse<string> data)
    {
        try
        {
            var doc = XDocument.Parse(data.Response);
            var mylist = doc.Descendants("mylist");
            if (mylist == null)
            {
                throw new UnexpectedHttpResponseException("mylist tag not found", data.Code, data.Response);
            }

            var items = mylist.Descendants("mylistitem");
            var responses = items.Select(
                item =>
                {
                    var id = (int?)item.Attribute("id");
                    var aid = (int?)item.Attribute("aid");
                    var eid = (int?)item.Attribute("eid");
                    var fid = (int?)item.Attribute("fid");
                    var updated = (DateTime?)null;
                    if (DateTime.TryParse(item.Attribute("updated")?.Value, out var tempu))
                    {
                        updated = tempu;
                    }

                    var viewed = (DateTime?)null;
                    if (DateTime.TryParse(item.Attribute("viewdate")?.Value, out var tempv))
                    {
                        viewed = tempv.ToLocalTime();
                    }

                    var stateI = (int?)item.Element("state");
                    var state = stateI.HasValue ? (MyList_State)stateI.Value : MyList_State.Unknown;
                    var fileStateElement = item.Descendants("filestate").FirstOrDefault()?.Value;
                    var fileState = MyList_FileState.Normal;
                    if (!string.IsNullOrWhiteSpace(fileStateElement) && int.TryParse(fileStateElement, out var fileStateParsed))
                    {
                        fileState = (MyList_FileState)fileStateParsed;
                    }

                    return new ResponseMyList
                    {
                        MyListID = id,
                        AnimeID = aid,
                        EpisodeID = eid,
                        FileID = fid,
                        UpdatedAt = updated,
                        ViewedAt = viewed,
                        State = state,
                        FileState = fileState
                    };
                }
            ).ToList();
            return new HttpResponse<List<ResponseMyList>> { Code = data.Code, Response = responses };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            return new HttpResponse<List<ResponseMyList>> { Code = data.Code, Response = null };
        }
    }

    public RequestMyList(IHttpConnectionHandler handler, ILoggerFactory loggerFactory, ISettingsProvider settingsProvider) : base(handler, loggerFactory)
    {
        var settings = settingsProvider.GetSettings().AniDb;
        _aniDBUrl = settings.ServerAddress;
        _aniDBPort = (ushort)(settings.ServerPort + 1);
    }
}
