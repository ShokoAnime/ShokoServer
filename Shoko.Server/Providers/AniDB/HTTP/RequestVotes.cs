using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class RequestVotes : HttpBaseRequest<List<ResponseVote>>
    {

        protected override string BaseCommand => $"http://api.anidb.net:9001/httpapi?client=animeplugin&clientver=1&protover=1&request=votes&user={Username}&pass={Password}";
        
        public string Username { private get; init; }
        public string Password { private get; init; }
        
        protected override HttpBaseResponse<List<ResponseVote>> ParseResponse(ILogger logger, HttpBaseResponse<string> data)
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
                
                return new HttpBaseResponse<List<ResponseVote>> {Code = data.Code, Response = results};
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Message}", ex.Message);
                return new HttpBaseResponse<List<ResponseVote>> {Code = data.Code, Response = null};
            }
        }

        private static ResponseVote GetAnime(XmlNode node)
        {
            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (!int.TryParse(node.Attributes?["aid"]?.Value, out var entityID)) return null;
            if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val)) return null;
            return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.Anime, VoteValue = val };
        }

        private static ResponseVote GetAnimeTemp(XmlNode node)
        {
            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (!int.TryParse(node.Attributes?["aid"]?.Value, out var entityID)) return null;
            if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val)) return null;
            return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.AnimeTemp, VoteValue = val };
        }

        private static ResponseVote GetEpisode(XmlNode node)
        {
            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (!int.TryParse(node.Attributes?["eid"]?.Value, out var entityID)) return null;
            if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var val)) return null;
            return new ResponseVote { EntityID = entityID, VoteType = AniDBVoteType.Episode, VoteValue = val };
        }
    }
}
