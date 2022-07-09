using System;
using System.Globalization;
using System.Xml;
using AniDBAPI;
using Microsoft.Extensions.Logging;

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

            return anime;
        }

        private XmlDocument ParseXml(string input)
        {
            var docAnime = new XmlDocument();
            docAnime.LoadXml(input);
            return docAnime;
        }

        private ResponseGetAnime ParseAnime(int animeID, XmlNode docAnime)
        {
            // most of the general anime data will be overwritten by the UDP command
            var anime = new ResponseGetAnime
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
            anime.AnimeTypeRAW = TryGetProperty(docAnime, "anime", "type");
            


            var episodeCount = TryGetProperty(docAnime, "anime", "episodecount");
            int.TryParse(episodeCount, out var epCount);
            anime.EpisodeCount = epCount;
            anime.EpisodeCountNormal = epCount;

            var dateString = TryGetProperty(docAnime, "anime", "startdate");

            anime.AirDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                {
                    anime.AirDate = date;
                }
                else if (DateTime.TryParseExact(dateString, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date2))
                {
                    anime.AirDate = date2;
                }
            }

            dateString = TryGetProperty(docAnime, "anime", "enddate");
            anime.EndDate = null;
            if (!string.IsNullOrEmpty(dateString))
            {
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                {
                    anime.EndDate = date;
                }
            }

            anime.BeginYear = anime.AirDate?.Year ?? 0;
            anime.EndYear = anime.EndDate?.Year ?? 0;

            var restricted = docAnime["anime"].Attributes["restricted"]?.Value;
            if (bool.TryParse(restricted, out var res))
                anime.Restricted = res ? 1 : 0;
            else
                anime.Restricted = 0;

            anime.URL = TryGetProperty(docAnime, "anime", "url");
            anime.Picname = TryGetProperty(docAnime, "anime", "picture");

            anime.DateTimeUpdated = DateTime.Now;
            anime.DateTimeDescUpdated = anime.DateTimeUpdated;

            #region Related Anime

            var raItems = docAnime["anime"]["relatedanime"]?.GetElementsByTagName("anime");
            if (raItems != null)
            {
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

            #endregion

            #region Titles

            var titleItems = docAnime["anime"]["titles"]?.GetElementsByTagName("title");
            if (titleItems != null)
            {
                foreach (XmlNode node in titleItems)
                {
                    var titleType = node?.Attributes?["type"]?.Value?.Trim().ToLower();
                    if (string.IsNullOrEmpty(titleType)) continue;
                    var languageType = node.Attributes["xml:lang"]?.Value?.Trim().ToLower();
                    if (string.IsNullOrEmpty(languageType)) continue;
                    var titleValue = node.InnerText.Trim();
                    if (string.IsNullOrEmpty(titleValue)) continue;

                    if (titleType.Trim().ToUpper().Equals("MAIN"))
                        anime.MainTitle = titleValue.Replace('`', '\'');
                }
            }

            #endregion

            #region Ratings

            // init ratings
            anime.VoteCount = 0;
            anime.TempVoteCount = 0;
            anime.Rating = 0;
            anime.TempRating = 0;
            anime.ReviewCount = 0;
            anime.AvgReviewRating = 0;

            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            var ratingItems = docAnime["anime"]["ratings"]?.ChildNodes;
            if (ratingItems == null) return anime;
            foreach (XmlNode node in ratingItems)
            {
                var name = node?.Name?.Trim().ToLower();
                if (string.IsNullOrEmpty(name)) continue;
                if (!int.TryParse(TryGetAttribute(node, "count"), out var iCount)) continue;
                if (!decimal.TryParse(node.InnerText.Trim(), style, culture, out var iRating)) continue;
                iRating = (int) Math.Round(iRating * 100);

                if (name.Equals("permanent"))
                {
                    anime.VoteCount = iCount;
                    anime.Rating = (int) iRating;
                }
                else if (name.Equals("temporary"))
                {
                    anime.TempVoteCount = iCount;
                    anime.TempRating = (int) iRating;
                }
                else if (name.Equals("review"))
                {
                    anime.ReviewCount = iCount;
                    anime.AvgReviewRating = (int) iRating;
                }
            }
            #endregion

            return anime;
        }
        
        public static RelationType ConvertRelationTypeTextToEnum(string relType)
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
    }
}
