using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Providers.AniDB.Http.GetAnime;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class HttpAnimeParser
    {
        private readonly ILogger<HttpAnimeParser> _logger;

        public HttpAnimeParser(ILogger<HttpAnimeParser> logger)
        {
            _logger = logger;
        }

        public ResponseGetAnime Parse(int animeId, string input)
        {
            var xml = ParseXml(input);
            if (xml == null) return null;
            var anime = ParseAnime(animeId, xml);
            if (anime == null) return null;
            var titles = ParseTitles(animeId, xml);
            var episodes = ParseEpisodes(animeId, xml);
            var tags = ParseTags(animeId, xml);
            var staff = ParseStaffs(animeId, xml);
            var characters = ParseCharacters(animeId, xml);
            var relations = ParseRelations(animeId, xml);
            var resources = ParseResources(animeId, xml);
            var similar = ParseSimilar(animeId, xml);

            var response = new ResponseGetAnime
            {
                Anime = anime,
                Titles = titles,
                Episodes = episodes,
                Tags = tags,
                Staff = staff,
                Characters = characters,
                Relations = relations,
                Resources = resources,
                Similar = similar,
            };
            return response;
        }

        private static XmlDocument ParseXml(string input)
        {
            var docAnime = new XmlDocument();
            docAnime.LoadXml(input);
            return docAnime;
        }

#region Parse Anime Details
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
                if (string.IsNullOrEmpty(node?.Attributes?["xml:lang"]?.Value.Trim().ToLower())) continue;
                var titleValue = node.InnerText.Trim();
                if (string.IsNullOrEmpty(titleValue)) continue;

                var titleType = node.Attributes?["type"]?.Value.Trim().ToLower();
                if (!"main".Equals(titleType)) continue;
                anime.MainTitle = titleValue.Replace('`', '\'');
            }
        }
#endregion
#region Parse Titles
        private List<ResponseTitle> ParseTitles(int animeID, XmlDocument docAnime)
        {
            var titles = new List<ResponseTitle>();

            var titleItems = docAnime?["anime"]?["titles"]?.GetElementsByTagName("title");
            if (titleItems == null) return titles;
            foreach (XmlNode node in titleItems)
            {
                try
                {
                    var animeTitle = ParseTitle(animeID, node);
                    titles.Add(animeTitle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return titles;
        }

        private static ResponseTitle ParseTitle(int animeID, XmlNode node)
        {
            var titleType = TryGetAttribute(node, "type");
            if (!Enum.TryParse(titleType, true, out TitleType type)) return null;
            var language = TryGetAttribute(node, "xml:lang");
            var langEnum = language.GetEnum();
            var title = node.InnerText.Trim().Replace('`', '\'');
            return new ResponseTitle { AnimeID = animeID, Title = title, TitleType = type, Language = langEnum };
        }
#endregion
#region Parse Episodes
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
                    _logger.LogError(exc, "{Ex}", exc.ToString());
                }
            }

            return episodes;
        }

        private static ResponseEpisode ParseEpisode(int animeID, XmlNode node)
        {
            if (!int.TryParse(node?.Attributes?["id"]?.Value, out var id))
                throw new UnexpectedHttpResponseException("Could not get episode ID from XML", HttpStatusCode.OK, node?.ToString());
            // default values

            var epNo = TryGetProperty(node, "epno");
            var episodeType = GetEpisodeType(epNo);
            var episodeNumber = GetEpisodeNumber(epNo, episodeType);

            var length = TryGetProperty(node, "length");
            int.TryParse(length, out var lMinutes);
            var secs = lMinutes * 60;

            const NumberStyles style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            decimal.TryParse(TryGetProperty(node, "rating"), style, culture, out var rating);
            int.TryParse(TryGetAttribute(node, "rating", "votes"), out var votes);

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

            var dateString = TryGetProperty(node, "airdate");
            var airDate = GetDate(dateString, true);
            var description = TryGetProperty(node, "summary")?.Replace('`', '\'');
            
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
#region Parse Tags
        private List<ResponseTag> ParseTags(int animeID, XmlNode docAnime)
        {
            var tags = new List<ResponseTag>();

            var tagItems = docAnime?["anime"]?["tags"]?.GetElementsByTagName("tag");
            if (tagItems == null) return tags;
            foreach (XmlNode node in tagItems)
            {
                try
                {
                    var tag = ParseTag(animeID, node);
                    if (tag == null) continue;
                    tags.Add(tag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return tags;
        }

        private static ResponseTag ParseTag(int animeID, XmlNode node)
        {
            if (!int.TryParse(TryGetAttribute(node, "id"), out var tagID)) return null;
            var tagName = TryGetProperty(node, "name")?.Replace('`', '\'');
            if (string.IsNullOrEmpty(tagName)) return null;
            var tagDescription = TryGetProperty(node, "description")?.Replace('`', '\'');
            int.TryParse(TryGetAttribute(node, "weight"), out var weight);
            bool.TryParse(TryGetAttribute(node, "localspoiler"), out var lsp);
            bool.TryParse(TryGetAttribute(node, "globalspoiler"), out var gsp);

            return new ResponseTag
            {
                AnimeID = animeID,
                TagID = tagID,
                TagName = tagName,
                TagDescription = tagDescription,
                Weight = weight,
                LocalSpoiler = lsp,
                GlobalSpoiler = gsp,
                Spoiler = lsp || gsp,
            };
        }
#endregion
#region Parse Staff
        private List<ResponseStaff> ParseStaffs(int animeID, XmlNode docAnime)
        {
            var creators = new List<ResponseStaff>();

            var charItems = docAnime?["anime"]?["creators"]?.GetElementsByTagName("name");
            if (charItems == null) return creators;
            foreach (XmlNode node in charItems)
            {
                try
                {
                    var staff = ParseStaff(animeID, node);
                    creators.Add(staff);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }
            return creators;
        }

        private static ResponseStaff ParseStaff(int animeID, XmlNode node)
        {
            if (!int.TryParse(TryGetAttribute(node, "id"), out var creatorID)) return null;
            var creatorType = TryGetAttribute(node, "type");
            var creatorName = node.InnerText.Replace('`', '\'');
            return new ResponseStaff
            {
                AnimeID = animeID,
                CreatorID = creatorID,
                CreatorName = creatorName,
                CreatorType = creatorType,
            };
        }
#endregion
#region Parse Characters
        private List<ResponseCharacter> ParseCharacters(int animeID, XmlNode docAnime)
        {
            var chars = new List<ResponseCharacter>();

            var charItems = docAnime?["anime"]?["characters"]?.GetElementsByTagName("character");
            if (charItems == null) return chars;
            foreach (XmlNode node in charItems)
            {
                try
                {
                    var chr = ParseCharacter(animeID, node);
                    chars.Add(chr);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return chars;
        }

        private static ResponseCharacter ParseCharacter(int animeID, XmlNode node)
        {
            if (int.TryParse(TryGetAttribute(node, "id"), out var charID)) return null;
            var charType = TryGetAttribute(node, "type");
            var charName = TryGetProperty(node, "name")?.Replace('`', '\'');
            var charDescription = TryGetProperty(node, "description")?.Replace('`', '\'');
            var picName = TryGetProperty(node, "picture");
            
            // parse seiyuus
            var seiyuus = new List<ResponseSeiyuu>();
            foreach (XmlNode nodeChild in node.ChildNodes)
            {
                if (nodeChild?.Name != "seiyuu") continue;
                if (!int.TryParse(nodeChild.Attributes?["id"]?.Value, out var seiyuuID)) continue;
                var seiyuuPic = nodeChild.Attributes["picture"]?.Value;
                var seiyuuName = nodeChild.InnerText.Replace('`', '\'');
                seiyuus.Add(new ResponseSeiyuu { SeiyuuID = seiyuuID, SeiyuuName = seiyuuName, PicName = seiyuuPic });
            }

            return new ResponseCharacter
            {
                AnimeID = animeID,
                CharacterID = charID,
                CharacterType = charType,
                CharacterName = charName,
                CharacterDescription = charDescription,
                PicName = picName,
                Seiyuus = seiyuus,
            };
        }
#endregion
#region Parse Resources
        private List<ResponseResource> ParseResources(int animeID, XmlNode docAnime)
        {
            var result = new List<ResponseResource>();
            var items = docAnime?["anime"]?["resources"]?.GetElementsByTagName("resource");
            if (items == null) return result;
            foreach (XmlNode node in items)
            {
                try
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        var resourceID = child["identifier"]?.InnerText ?? child["url"]?.InnerText;
                        if (!int.TryParse(TryGetAttribute(node, "type"), out var typeInt)) continue;
                        var resource = new ResponseResource { AnimeID = animeID, ResourceID = resourceID, ResourceType = (AniDB_ResourceLinkType)typeInt };
                        result.Add(resource);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return result;
        }
#endregion
#region Parse Relations
        private List<ResponseRelation> ParseRelations(int animeID, XmlNode docAnime)
        {
            var rels = new List<ResponseRelation>();

            var relItems = docAnime?["anime"]?["relatedanime"]?.GetElementsByTagName("anime");
            if (relItems == null) return rels;
            foreach (XmlNode node in relItems)
            {
                try
                {
                    if (!int.TryParse(TryGetAttribute(node, "id"), out var id)) continue;
                    var type = TryGetAttribute(node, "type");
                    var relationType = type.ToLowerInvariant() switch
                    {
                        "prequel" => RelationType.Prequel,
                        "sequel" => RelationType.Sequel,
                        "parent story" => RelationType.MainStory,
                        "side story" => RelationType.SideStory,
                        "full story" => RelationType.FullStory,
                        "summary" => RelationType.Summary,
                        "other" => RelationType.Other,
                        "alternative setting" => RelationType.AlternativeSetting,
                        "same setting" => RelationType.SameSetting,
                        "character" => RelationType.SharedCharacters,
                        _ => RelationType.Other,
                    };
                    var relation = new ResponseRelation { AnimeID = animeID, RelationType = relationType, RelatedAnimeID = id};
                    rels.Add(relation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return rels;
        }
#endregion
#region Parse Similar
        private List<ResponseSimilar> ParseSimilar(int animeID, XmlNode docAnime)
        {
            var rels = new List<ResponseSimilar>();

            var simItems = docAnime["anime"]?["similaranime"]?.GetElementsByTagName("anime");
            if (simItems == null) return rels;
            foreach (XmlNode node in simItems)
            {
                try
                {
                    if (!int.TryParse(TryGetAttribute(node, "id"), out int id)) continue;

                    int.TryParse(TryGetAttribute(node, "approval"), out int appr);

                    int.TryParse(TryGetAttribute(node, "total"), out int tot);
                    var sim = new ResponseSimilar { AnimeID = animeID, SimilarAnimeID = id, Approval = appr, Total = tot };
                    rels.Add(sim);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Ex}", ex.ToString());
                }
            }

            return rels;
        }
#endregion
#region XML Utils
        private static string TryGetProperty(XmlNode doc, string keyName, string propertyName)
        {
            if (doc == null || string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(propertyName)) return string.Empty;
            return doc[keyName]?[propertyName]?.InnerText.Trim() ?? string.Empty;
        }

        private static string TryGetProperty(XmlNode node, string propertyName)
        {
            if (node == null || string.IsNullOrEmpty(propertyName)) return string.Empty;
            return node[propertyName]?.InnerText.Trim() ?? string.Empty;
        }

        private static string TryGetAttribute(XmlNode parentnode, string nodeName, string attName)
        {
            if (parentnode == null || string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(attName))
                return string.Empty;
            return parentnode[nodeName]?.Attributes[attName]?.Value ?? string.Empty;
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
