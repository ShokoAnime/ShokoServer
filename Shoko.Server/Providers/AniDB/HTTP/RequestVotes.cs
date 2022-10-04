using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class RequestVotes : HttpRequest<List<ResponseVote>>
{
    protected override string BaseCommand =>
        $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=votes&user={Username}&pass={Password}";

    public string Username { private get; set; }
    public string Password { private get; set; }

    protected override HttpResponse<List<ResponseVote>> ParseResponse(HttpResponse<string> data)
    {
        var results = new List<ResponseVote>();
        try
        {
            var docAnime = new XmlDocument();
            docAnime.LoadXml(data.Response);
            var myitems = docAnime["votes"]?["anime"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetAnime).Where(vote => vote != null));
            }

            // get the temporary anime votes
            myitems = docAnime["votes"]?["animetemporary"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetAnimeTemp).Where(vote => vote != null));
            }

            // get the episode votes
            myitems = docAnime["votes"]?["episode"]?.GetElementsByTagName("vote");
            if (myitems != null)
            {
                results.AddRange(myitems.Cast<XmlNode>().Select(GetEpisode).Where(vote => vote != null));
            }

            return new HttpResponse<List<ResponseVote>> { Code = data.Code, Response = results };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Message}", ex.Message);
            return new HttpResponse<List<ResponseVote>> { Code = data.Code, Response = null };
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

        if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.Anime, VoteValue = val };
    }

    private static ResponseVote GetAnimeTemp(XmlNode node)
    {
        const NumberStyles style = NumberStyles.Number;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");

        if (!int.TryParse(node.Attributes?["aid"]?.Value, out var entityID))
        {
            return null;
        }

        if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.AnimeTemp, VoteValue = val };
    }

    private static ResponseVote GetEpisode(XmlNode node)
    {
        const NumberStyles style = NumberStyles.Number;
        var culture = CultureInfo.CreateSpecificCulture("en-GB");

        if (!int.TryParse(node.Attributes?["eid"]?.Value, out var entityID))
        {
            return null;
        }

        if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val))
        {
            return null;
        }

        return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.Episode, VoteValue = val };
    }

    public RequestVotes(IHttpConnectionHandler handler, ILoggerFactory loggerFactory) : base(handler, loggerFactory)
    {
    }
}
