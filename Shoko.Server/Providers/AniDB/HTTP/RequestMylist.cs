using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class RequestMylist : HttpRequest<List<MylistEntry>>
{
    protected override string BaseCommand => $"httpapi?client=animeplugin&clientver=1&protover=1&request=mylist&user={Username}&pass={Password}";

    public string Username { private get; set; } = null!;
    public string Password { private get; set; } = null!;

    protected override Task<HttpResponse<List<MylistEntry>>> ParseResponse(HttpResponse<string> data)
    {
        try
        {
            var doc = XDocument.Parse(data.Response);
            var mylist = doc.Descendants("mylist")?.FirstOrDefault();
            if (mylist is null)
            {
                var error = doc.Descendants("error").FirstOrDefault();
                if (error is not null)
                {
                    var errorCode = (int)error.Attribute("value")!;
                    if (errorCode == 330) // 'mylist empty'
                    {
                        Logger.LogTrace("Mylist is empty.");
                        return Task.FromResult(new HttpResponse<List<MylistEntry>> { Code = data.Code, Response = [] });
                    }
                }

                throw new UnexpectedHttpResponseException("mylist tag not found", data.Code, data.Response);
            }

            var uid = (ulong)mylist.Attribute("uid")!;
            var items = mylist.Descendants("mylistitem");
            var responses = items.Select(
                item =>
                {
                    var lid = (ulong)item.Attribute("id")!;
                    var aid = (int)item.Attribute("aid")!;
                    var eid = (int)item.Attribute("eid")!;
                    var fid = (int)item.Attribute("fid")!;
                    var updated = DateOnly.Parse(item.Attribute("updated")!.Value);
                    var viewed = (DateTime?)null;
                    if (DateTime.TryParse(item.Attribute("viewdate")?.Value, out var tempv))
                    {
                        // kept in UTC; the desired watched date is always UTC, and the
                        // two are compared directly
                        viewed = tempv.ToUniversalTime();
                    }

                    var stateI = (int?)item.Element("state");
                    var state = stateI.HasValue ? (MylistState)stateI.Value : MylistState.Unknown;
                    var fileStateElement = item.Element("filestate")?.Value;
                    var fileState = MylistFileState.Normal;
                    if (!string.IsNullOrWhiteSpace(fileStateElement) && int.TryParse(fileStateElement, out var fileStateParsed))
                    {
                        fileState = (MylistFileState)fileStateParsed;
                    }

                    return new MylistEntry
                    {
                        Username = Username,
                        UserID = uid,
                        MylistID = lid,
                        AnimeID = aid,
                        EpisodeID = eid,
                        FileID = fid,
                        UpdatedAt = updated,
                        ViewedAt = viewed,
                        IsViewed = viewed is not null,
                        State = state,
                        FileState = fileState
                    };
                }
            ).ToList();
            return Task.FromResult(new HttpResponse<List<MylistEntry>> { Code = data.Code, Response = responses });
        }
        catch (UnexpectedHttpResponseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // swallowing this used to leave the caller with a null response and no
            // way to tell a malformed document apart from an empty Mylist, which
            // silently stopped the cache from ever refreshing again
            Logger.LogError(ex, "Failed to parse the Mylist response");
            throw new UnexpectedHttpResponseException($"Failed to parse the Mylist response: {ex.Message}", data.Code, data.Response);
        }
    }

    public RequestMylist(IHttpConnectionHandler handler, ILoggerFactory loggerFactory, ISettingsProvider settingsProvider) : base(handler, loggerFactory) { }
}
