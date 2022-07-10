using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml;
using AniDBAPI;
using Microsoft.Extensions.Logging;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Http.GetAnime;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class HttpParser
    {
        private readonly ILogger<HttpParser> _logger;

        public HttpParser(ILogger<HttpParser> logger)
        {
            _logger = logger;
        }

        public ResponseGetAnime Parse(int animeId, string input)
        {
            var rawXml = input.Trim();
            APIUtils.WriteAnimeHTTPToFile(animeId, rawXml);

            var xml = ParseXml(input);
            var anime = ParseAnime(animeId, xml);
            var episodes = ParseEpisodes(animeId, xml);

            var response = new ResponseGetAnime { Anime = anime, Episodes = episodes };
            return response;
        }

        private static XmlDocument ParseXml(string input)
        {
            var docAnime = new XmlDocument();
            docAnime.LoadXml(input);
            return docAnime;
        }

#region ParseAnime
        private ResponseAnime ParseAnime(int animeID, XmlNode docAnime)
        {
            // most of the general anime data will be overwritten by the UDP command
            var anime = new ResponseAnime
            {
                AnimeID = animeID,
            };

            // check if there is any data
            if (docAnime?["anime"]?.Attributes["id"]?.Value == null)
            {
                _logger.LogWarning("AniDB ProcessAnimeDetails - Received no or invalid info in XML");
                return null;
            }

            anime.Description = TryGetProperty(docAnime, "anime", "description")?.Replace('`', '\'');
            var type = TryGetProperty(docAnime, "anime", "type");
            anime.AnimeType = type.ToLowerInvariant() switch
            {
                "movie" => AnimeType.Movie,
                "ova" => AnimeType.OVA,
                "tv series" => AnimeType.TVSeries,
                "tv special" => AnimeType.TVSpecial,
                "web" => AnimeType.Web,
                _ => AnimeType.Other,
            };

            var episodeCount = TryGetProperty(docAnime, "anime", "episodecount");
            int.TryParse(episodeCount, out var epCount);
            anime.EpisodeCount = epCount;
            anime.EpisodeCountNormal = epCount;

            ParseDates(docAnime, anime);

            var restricted = docAnime["anime"].Attributes["restricted"]?.Value;
            if (bool.TryParse(restricted, out var res))
                anime.Restricted = res ? 1 : 0;
            else
                anime.Restricted = 0;

            anime.URL = TryGetProperty(docAnime, "anime", "url");
            anime.Picname = TryGetProperty(docAnime, "anime", "picture");

            ParseRelatedAnime(docAnime, anime);
            ParseMainTitle(docAnime, anime);
            ParseRatings(docAnime, anime);

            return anime;
        }

        private static void ParseDates(XmlNode docAnime, ResponseAnime anime)
        {
            var dateString = TryGetProperty(docAnime, "anime", "startdate");
            anime.AirDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(
                        dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var date
                    ))
                {
                    anime.AirDate = date;
                }
                else if (DateTime.TryParseExact(
                             dateString, "yyyy-MM", CultureInfo.InvariantCulture,
                             DateTimeStyles.AssumeUniversal, out var date2
                         ))
                {
                    anime.AirDate = date2;
                }
            }

            dateString = TryGetProperty(docAnime, "anime", "enddate");
            anime.EndDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(
                        dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var date
                    ))
                {
                    anime.EndDate = date;
                }
            }

            anime.BeginYear = anime.AirDate?.Year ?? 0;
            anime.EndYear = anime.EndDate?.Year ?? 0;
        }

        private static void ParseRatings(XmlNode docAnime, ResponseAnime anime)
        {
            // init ratings
            anime.VoteCount = 0;
            anime.TempVoteCount = 0;
            anime.Rating = 0;
            anime.TempRating = 0;
            anime.ReviewCount = 0;
            anime.AvgReviewRating = 0;

            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            var ratingItems = docAnime["anime"]["ratings"]?.ChildNodes;
            if (ratingItems == null) return;

            foreach (XmlNode node in ratingItems)
            {
                var name = node?.Name.Trim().ToLower();
                if (string.IsNullOrEmpty(name)) continue;
                if (!int.TryParse(TryGetAttribute(node, "count"), out var iCount)) continue;
                if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var iRating)) continue;
                iRating = (int)Math.Round(iRating * 100);

                switch (name)
                {
                    case "permanent":
                        anime.VoteCount = iCount;
                        anime.Rating = (int)iRating;
                        break;
                    case "temporary":
                        anime.TempVoteCount = iCount;
                        anime.TempRating = (int)iRating;
                        break;
                    case "review":
                        anime.ReviewCount = iCount;
                        anime.AvgReviewRating = (int)iRating;
                        break;
                }
            }
        }

        private static void ParseMainTitle(XmlNode docAnime, ResponseAnime anime)
        {
            var titleItems = docAnime["anime"]["titles"]?.GetElementsByTagName("title");
            if (titleItems == null) return;
            foreach (XmlNode node in titleItems)
            {
                var titleType = node?.Attributes?["type"]?.Value.Trim().ToLower();
                if (string.IsNullOrEmpty(titleType)) continue;
                var languageType = node.Attributes["xml:lang"]?.Value.Trim().ToLower();
                if (string.IsNullOrEmpty(languageType)) continue;
                var titleValue = node.InnerText.Trim();
                if (string.IsNullOrEmpty(titleValue)) continue;

                if (titleType.Trim().ToUpper().Equals("MAIN"))
                    anime.MainTitle = titleValue.Replace('`', '\'');
            }
        }

        private static void ParseRelatedAnime(XmlNode docAnime, ResponseAnime anime)
        {
            var raItems = docAnime["anime"]["relatedanime"]?.GetElementsByTagName("anime");
            if (raItems == null) return;
            anime.RelatedAnimeIdsRAW = string.Empty;
            anime.RelatedAnimeTypesRAW = string.Empty;

            foreach (XmlNode node in raItems)
            {
                if (node?.Attributes?["id"]?.Value == null) continue;
                if (!int.TryParse(node.Attributes["id"].Value, out var id)) continue;
                var relType = ConvertRelationTypeTextToEnum(TryGetAttribute(node, "type"));

                if (anime.RelatedAnimeIdsRAW.Length > 0) anime.RelatedAnimeIdsRAW += "'";
                if (anime.RelatedAnimeTypesRAW.Length > 0) anime.RelatedAnimeTypesRAW += "'";

                anime.RelatedAnimeIdsRAW += id.ToString();
                anime.RelatedAnimeTypesRAW += relType.ToString();
            }
        }

        private static RelationType ConvertRelationTypeTextToEnum(string relType)
        {
            var type = relType.Trim().ToLower();
            return type switch
            {
                "sequel" => RelationType.Sequel,
                "prequel" => RelationType.Prequel,
                "same setting" => RelationType.SameSetting,
                "alternative setting" => RelationType.AlternativeSetting,
                "alternative version" => RelationType.AlternativeVersion,
                "music video" => RelationType.MusicVideo,
                "character" => RelationType.SameSetting,
                "side story" => RelationType.SideStory,
                "parent story" => RelationType.MainStory,
                "summary" => RelationType.Summary,
                "full story" => RelationType.FullStory,
                _ => RelationType.Other,
            };
        }
#endregion
#region ParseEpisodes
        private List<ResponseEpisode> ParseEpisodes(int animeID, XmlNode docAnime)
        {
            var episodes = new List<ResponseEpisode>();
            var episodeItems = docAnime?["anime"]?["episodes"]?.GetElementsByTagName("episode");
            if (episodeItems == null) return episodes;
            foreach (XmlNode node in episodeItems)
            {
                try
                {
                    var ep = ParseEpisode(animeID, node);

                    episodes.Add(ep);
                }
                catch (Exception exc)
                {
                    _logger.LogError(exc, exc.ToString());
                }
            }

            return episodes;
        }

        private static ResponseEpisode ParseEpisode(int animeID, XmlNode node)
        {
            if (!int.TryParse(node?.Attributes?["id"]?.Value, out var id))
                throw new UnexpectedHttpResponseException("Could not get episode ID from XML", HttpStatusCode.OK, node?.ToString());
            // default values

            var epNo = AniDBHTTPHelper.TryGetProperty(node, "epno");
            var episodeType = GetEpisodeType(epNo);
            var episodeNumber = GetEpisodeNumber(epNo, episodeType);

            var length = AniDBHTTPHelper.TryGetProperty(node, "length");
            int.TryParse(length, out var lMinutes);
            var secs = lMinutes * 60;

            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            decimal.TryParse(AniDBHTTPHelper.TryGetProperty(node, "rating"), style, culture, out var rating);
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "rating", "votes"), out var votes);

            var titles = node.ChildNodes.Cast<XmlNode>()
                .Select(nodeChild => new
                        {
                            nodeChild,
                            episodeTitle = new AniDB_Episode_Title
                            {
                                AniDB_EpisodeID = id,
                                Language = nodeChild?.Attributes?["xml:lang"]?.Value.Trim().ToUpperInvariant(),
                                Title = nodeChild?.InnerText.Trim().Replace('`', '\''),
                            },
                        })
                .Where(t => Equals("title", t.nodeChild?.Name) && !string.IsNullOrEmpty(t.nodeChild.InnerText) && !string.IsNullOrEmpty(t.episodeTitle.Language))
                .Select(t => t.episodeTitle).ToList();

            var dateString = AniDBHTTPHelper.TryGetProperty(node, "airdate");
            var airDate = GetDate(dateString, true);
            var description = AniDBHTTPHelper.TryGetProperty(node, "summary")?.Replace('`', '\'');
            
            return new ResponseEpisode
            {
                Description = description,
                EpisodeNumber = episodeNumber,
                EpisodeType = episodeType,
                Rating = rating,
                LengthSeconds = secs,
                Votes = votes,
                EpisodeID = id,
                AnimeID = animeID,
                AirDate = airDate,
                Titles = titles,
            };
        }
        
        private static int GetEpisodeNumber(string fld, EpisodeType epType)
        {
            // if it is NOT a normal episode strip the leading character
            var fldTemp = fld.Trim();
            if (epType != EpisodeType.Episode)
                fldTemp = fldTemp[1..^1];

            if (int.TryParse(fldTemp, out var epno)) return epno;
            // if we couldn't convert to an int, it must mean it is a double episode
            // we will just take the first ep as the episode number
            var sDetails = fldTemp!.Split('-');
            epno = int.Parse(sDetails[0]);
            return epno;
        }

        private static EpisodeType GetEpisodeType(string fld)
        {
            // if the first char is a numeric than it is a normal episode
            if (int.TryParse(fld.Trim()[..1], out _))
                return EpisodeType.Episode;
            // the first character should contain the type of special episode
            // S(special), C(credits), T(trailer), P(parody), O(other)
            // we will just take this and store it in the database
            // this will allow for the user customizing how it is displayed on screen later
            var epType = fld.Trim()[..1].ToUpper();

            return epType switch
            {
                "C" => EpisodeType.Credits,
                "S" => EpisodeType.Special,
                "O" => EpisodeType.Other,
                "T" => EpisodeType.Trailer,
                "P" => EpisodeType.Parody,
                _ => EpisodeType.Episode,
            };
        }
#endregion
#region XML Utils
        private static string TryGetProperty(XmlNode doc, string keyName, string propertyName)
        {
            if (doc == null || string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(propertyName)) return string.Empty;
            return doc[keyName]?[propertyName]?.InnerText.Trim() ?? string.Empty;
        }

        private static string TryGetAttribute(XmlNode node, string attName)
        {
            if (node == null || string.IsNullOrEmpty(attName)) return string.Empty;
            return node.Attributes?[attName]?.Value ?? string.Empty;
        }

        private static DateTime? GetDate(string dateXml, bool isStartDate)
        {
            // eg "2008-12-31" or "2008-12" or "2008"
            if (dateXml == null || dateXml.Trim().Length < 4) return DateTime.UnixEpoch;

            var year = int.Parse(dateXml.Trim()[..4]);
            var month = dateXml.Trim().Length > 4 ? int.Parse(dateXml.Trim().Substring(5, 2)) : isStartDate ? 1 : 12;
            var day = dateXml.Trim().Length > 7 ? int.Parse(dateXml.Trim().Substring(8, 2)) : isStartDate ? 1 : DateTime.DaysInMonth(year, month);

            return new DateTime(year, month, day, 0, 0, 0);
        }

#endregion
    }
}
