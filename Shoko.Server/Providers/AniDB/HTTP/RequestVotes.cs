using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class RequestVotes : HttpRequest<List<ResponseVote>>
{
    protected override string BaseCommand =>
        $"httpapi?client=animeplugin&clientver=1&protover=1&request=votes&user={Username}&pass={Password}";

    public string Username { private get; set; }

    public string Password { private get; set; }

    protected override Task<HttpResponse<List<ResponseVote>>> ParseResponse(HttpResponse<string> data)
    {
        var results = new List<ResponseVote>();
        try
        {
            var docAnime = new XmlDocument();
            docAnime.LoadXml(data.Response);
            var myitems = docAnime["votes"]?["anime"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetAnime).WhereNotNull());
            }

            // get the temporary anime votes
            myitems = docAnime["votes"]?["animetemporary"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetAnimeTemp).WhereNotNull());
            }

            // get the episode votes
            myitems = docAnime["votes"]?["episode"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetEpisode).WhereNotNull());
            }

            return Task.FromResult(new HttpResponse<List<ResponseVote>> { Code = data.Code, Response = results });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Message}", ex.Message);
            return Task.FromResult(new HttpResponse<List<ResponseVote>> { Code = data.Code, Response = null });
        }
    }

    private static ResponseVote GetAnime(XmlNode node)
    {
        const NumberStyles style = NumberStyles.Number;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");

        if (!int.TryParse(node.Attributes?["aid"]?.Value, out var entityID))
        {
            return null;
        }

        if (!double.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = VoteType.AnimePermanent, VoteValue = val };
    }

    private static ResponseVote GetAnimeTemp(XmlNode node)
    {
        const NumberStyles style = NumberStyles.Number;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");

        if (!int.TryParse(node.Attributes?["aid"]?.Value, out var entityID))
        {
            return null;
        }

        if (!double.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = VoteType.AnimeTemporary, VoteValue = val };
    }

    private static ResponseVote GetEpisode(XmlNode node)
    {
        const NumberStyles style = NumberStyles.Number;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");

        if (!int.TryParse(node.Attributes?["eid"]?.Value, out var entityID))
        {
            return null;
        }

        if (!double.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = VoteType.Episode, VoteValue = val };
    }

    public RequestVotes(IHttpConnectionHandler handler, ILoggerFactory loggerFactory, ISettingsProvider settingsProvider) : base(handler, loggerFactory) { }
}
