using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.AniDB_API.Titles;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class SeriesController : BaseController
    {
        /// <summary>
        /// Get a list of all series available to the current user
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<Series>> GetAllSeries()
        {
            var allSeries = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a));
            return allSeries.Select(a => new Series(HttpContext, a)).OrderBy(a => a.Name).ToList();
        }

        /// <summary>
        /// Get the series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}")]
        public ActionResult<Series> GetSeries([FromRoute] int seriesID)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return new Series(HttpContext, ser);
        }

        /// <summary>
        /// Get AniDB Info for series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/AniDB")]
        public ActionResult<Series.AniDB> GetSeriesAniDBDetails([FromRoute] int seriesID)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return new Series.AniDB(HttpContext, anime);
        }

        /// <summary>
        /// Get AniDB Info from the AniDB ID
        /// </summary>
        /// <param name="anidbID">AniDB ID</param>
        /// <returns></returns>
        [HttpGet("AniDB/{anidbID}")]
        public ActionResult<Series.AniDB> GetSeriesAnidbDetailsByAnidbID([FromRoute] int anidbID)
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(anidbID);
            if (anime == null) return NotFound("No Series.AniDB entry for the given id");
            if (!User.AllowedAnime(anime)) return Forbid("Accessing Series.AniDB is not allowed for the current user");
            return new Series.AniDB(HttpContext, anime);
        }

        /// <summary>
        /// Get a Series from the AniDB ID
        /// </summary>
        /// <param name="anidbID">AniDB ID</param>
        /// <returns></returns>
        [HttpGet("AniDB/{anidbID}/Series")]
        public ActionResult<Series> GetSeriesByAniDBID([FromRoute] int anidbID)
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(anidbID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return new Series(HttpContext, ser);
        }

        /// <summary>
        /// Queue a refresh of the AniDB Info for series with AniDB ID
        /// </summary>
        /// <param name="anidbID">AniDB ID</param>
        /// <param name="force">Forcefully retrive updated data from AniDB</param>
        /// <param name="downloadRelations">Download relations for the series</param>
        /// <param name="createSeriesEntry">Also create the Series entries if they doesn't exist</param>
        /// <param name="immediate">Try to immediately refresh the data if we're not HTTP banned.</param>
        /// <returns>True if the refresh is done, otherwise false if it was queued.</returns>
        [HttpPost("AniDB/{anidbID}/Refresh")]
        public ActionResult<bool> QueueAniDBRefreshFromAniDBID([FromRoute] int anidbID, [FromQuery] bool force = false, [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null, [FromQuery] bool immediate = false)
        {
            if (!createSeriesEntry.HasValue) {
                createSeriesEntry = ServerSettings.Instance.AniDb.AutomaticallyImportSeries;
            }
            return Series.QueueAniDBRefresh(anidbID, force, downloadRelations, createSeriesEntry.Value, immediate);
        }

        /// <summary>
        /// Queue a refresh of the AniDB Info for series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="force">Forcefully retrive updated data from AniDB</param>
        /// <param name="downloadRelations">Download relations for the series</param>
        /// <param name="createSeriesEntry">Create the Series entries for related series if they doesn't exist</param>
        /// <param name="immediate">Try to immediately refresh the data if we're not HTTP banned.</param>
        /// <returns>True if the refresh is done, otherwise false if it was queued.</returns>
        [HttpPost("{seriesID}/AniDB/Refresh")]
        public ActionResult<bool> QueueAniDBRefresh([FromRoute] int seriesID, [FromQuery] bool force = false, [FromQuery] bool downloadRelations = false, [FromQuery] bool? createSeriesEntry = null, [FromQuery] bool immediate = false)
        {
            if (!createSeriesEntry.HasValue) {
                createSeriesEntry = ServerSettings.Instance.AniDb.AutomaticallyImportSeries;
            }
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.QueueAniDBRefresh(anime.AnimeID, force, downloadRelations, createSeriesEntry.Value, immediate);
        }

        /// <summary>
        /// Forcefully refresh the AniDB Info from XML on disk for series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <returns>True if the refresh is done, otherwise false if it failed.</returns>
        [HttpPost("{seriesID}/AniDB/Refresh/ForceFromXML")]
        public ActionResult<bool> RefreshAniDBFromXML([FromRoute] int seriesID)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.RefreshAniDBFromCachedXML(HttpContext, anime);
        }

        /// <summary>
        /// Add a permanent or temprary user-submitted rating for the series.
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="vote"></param>
        /// <returns></returns>
        [HttpPost("{seriesID}/Vote")]
        public ActionResult PostSeriesUserVote([FromRoute] int seriesID, [FromBody] Vote vote)
        {
            if (vote.Value < 0)
                return BadRequest("Value must be greater than or equal to 0.");
            if (vote.Value > vote.MaxValue)
                return BadRequest($"Value must be less than or equal to the set max value ({vote.MaxValue}).");
            if (vote.MaxValue <= 0)
                return BadRequest("Max value must be an integer above 0.");
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            Series.AddSeriesVote(HttpContext, ser, User.JMMUserID, vote);
            return NoContent();
        }

        /// <summary>
        /// Get TvDB Info for series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/TvDB")]
        public ActionResult<List<Series.TvDB>> GetSeriesTvDBDetails([FromRoute] int seriesID)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return Series.GetTvDBInfo(HttpContext, ser);
        }

        /// <summary>
        /// Get all images for series with ID, optionally with Disabled images, as well.
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="includeDisabled"></param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Images")]
        public ActionResult<Images> GetSeriesImages([FromRoute] int seriesID, [FromQuery] bool includeDisabled)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return Series.GetArt(HttpContext, ser.AniDB_ID, includeDisabled);
        }

        /// <summary>
        /// Get all images for series with ID, optionally with Disabled images, as well.
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="includeDisabled"></param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Images/{IncludeDisabled}")]
        [Obsolete]
        public ActionResult<Images> GetSeriesImagesFromPath([FromRoute] int seriesID, [FromRoute] bool includeDisabled)
            => GetSeriesImages(seriesID, includeDisabled);

        /// <summary>
        /// Get the default poster for a series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Images/Poster")]
        public ActionResult<Image> GetSeriesDefaultPoster([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultImage = Series.GetDefaultImage(series.AniDB_ID, ImageSizeType.Poster);
            if (defaultImage != null)
            {
                return defaultImage;
            }
            var images = Series.GetArt(HttpContext, series.AniDB_ID);
            return images.Posters.FirstOrDefault();
        }

        /// <summary>
        /// Set the default poster for a series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <param name="body">The body containing the source and id used to set the poster.</param>
        /// <returns></returns>
        [HttpPatch("{seriesID}/Images/Poster")]
        public ActionResult<Image> SetSeriesDefaultPoster([FromRoute] int seriesID, [FromBody] Image.Input.DefaultImageBody body)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");
            
            if (!int.TryParse(body.ID, out var imageID))
                return BadRequest("Invalid body id. Id must be a stringified int.");
            
            var imageEntityType = Image.GetImageTypeFromSourceAndType(body.Source, "Poster");
            if (!imageEntityType.HasValue)
                return BadRequest("Invalid body source");
            
            switch (imageEntityType.Value) {
                case ImageEntityType.AniDB_Cover: {
                    if (imageID != series.AniDB_ID)
                        return BadRequest("Invalid image id for selected source and id");
                    break;
                }
                case ImageEntityType.TvDB_Cover: {
                    var poster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                    if (poster == null)
                        return BadRequest("Invalid image id for selected source and id");
                    if (poster.Enabled != 1)
                        return BadRequest("Image is disabled");
                    break;
                }
                case ImageEntityType.MovieDB_Poster: {
                    var poster = RepoFactory.MovieDB_Poster.GetByID(imageID);
                    if (poster == null)
                        return BadRequest("Invalid image id for selected source and id");
                    if (poster.Enabled != 1)
                        return BadRequest("Image is disabled");
                    break;
                }
                default:
                    return BadRequest("Invalid source");
            }

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.Poster) ?? new() { AnimeID = series.AniDB_ID };

            defaultImage.ImageParentID = imageID;
            defaultImage.ImageParentType = (int) imageEntityType.Value;
            defaultImage.ImageType = (int) ImageSizeType.Poster;
            RepoFactory.AniDB_Anime_DefaultImage.Save(defaultImage);
            return new Image(imageID, imageEntityType.Value, true);
        }

        /// <summary>
        /// Unset the default poster for a series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <returns></returns>
        [HttpDelete("{seriesID}/Images/Poster")]
        public ActionResult DeleteSeriesDefaultPoster([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.Poster);
            if (defaultImage == null)
                return BadRequest("No default poster.");

            RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
            
            // Update the contract data (used by Shoko Desktop).
            RepoFactory.AnimeSeries.Save(series, false);
            return NoContent();
        }

        /// <summary>
        /// Get the default banner for a series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Images/Banner")]
        public ActionResult<Image> GetSeriesDefaultBanner([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultBanner = Series.GetDefaultImage(series.AniDB_ID, ImageSizeType.WideBanner);
            if (defaultBanner != null)
            {
                return defaultBanner;
            }
            var images = Series.GetArt(HttpContext, series.AniDB_ID);
            return images.Banners.FirstOrDefault();
        }

        /// <summary>
        /// Set the default banner for a series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <param name="body">The body containing the source and id used to set the banner.</param>
        /// <returns></returns>
        [HttpPatch("{seriesID}/Images/Banner")]
        public ActionResult<Image> SetSeriesDefaultBanner([FromRoute] int seriesID, [FromBody] Image.Input.DefaultImageBody body)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");
            
            if (!int.TryParse(body.ID, out var imageID))
                return BadRequest("Invalid body id. Id must be a stringified int.");
            
            var imageEntityType = Image.GetImageTypeFromSourceAndType(body.Source, "Banner");
            if (!imageEntityType.HasValue)
                return BadRequest("Invalid body source");
            
            switch (imageEntityType.Value) {
                case ImageEntityType.TvDB_Banner:
                    var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                    if (wideBanner == null)
                        return BadRequest("Invalid image id for selected source and id");
                    if (wideBanner.Enabled != 1)
                        return BadRequest("Image is disabled");
                    break;
                default:
                    return BadRequest("Invalid source");
            }

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.WideBanner) ?? new() { AnimeID = series.AniDB_ID };

            defaultImage.ImageParentID = imageID;
            defaultImage.ImageParentType = (int) imageEntityType.Value;
            defaultImage.ImageType = (int) ImageSizeType.WideBanner;
            RepoFactory.AniDB_Anime_DefaultImage.Save(defaultImage);
            return new Image(imageID, imageEntityType.Value, true);
        }

        /// <summary>
        /// Unset the default banner for a series.
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns></returns>
        [HttpDelete("{seriesID}/Images/Banner")]
        public ActionResult DeleteSeriesDefaultBanner([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.WideBanner);
            if (defaultImage == null)
                return BadRequest("No default banner.");

            RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
            
            // Update the contract data (used by Shoko Desktop).
            RepoFactory.AnimeSeries.Save(series, false);
            return NoContent();
        }

        /// <summary>
        /// Get the default fanart for the series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Images/Fanart")]
        public ActionResult<Image> GetSeriesDefaultFanart([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultImage = Series.GetDefaultImage(series.AniDB_ID, ImageSizeType.Fanart);
            if (defaultImage != null)
            {
                return defaultImage;
            }
            var images = Series.GetArt(HttpContext, series.AniDB_ID);
            return images.Fanarts.FirstOrDefault();
        }
        
        /// <summary>
        /// Set the default fanart for the series.
        /// </summary>
        /// <param name="seriesID">Series ID</param>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPatch("{seriesID}/Images/Fanart")]
        public ActionResult<Image> SetSeriesDefaultFanart([FromRoute] int seriesID, [FromBody] Image.Input.DefaultImageBody body)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");
            
            if (!int.TryParse(body.ID, out var imageID))
                return BadRequest("Invalid body id. Id must be a stringified int.");
            
            var imageEntityType = Image.GetImageTypeFromSourceAndType(body.Source, "Fanart");
            if (!imageEntityType.HasValue)
                return BadRequest("Invalid body source");
            
            switch (imageEntityType.Value) {
                case ImageEntityType.TvDB_FanArt: {
                    var fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                    if (fanart == null)
                        return BadRequest("Invalid image id for selected source and id");
                    if (fanart.Enabled != 1)
                        return BadRequest("Image is disabled");
                    break;
                }
                case ImageEntityType.MovieDB_FanArt: {
                    var fanart = RepoFactory.MovieDB_Fanart.GetByID(imageID);
                    if (fanart == null)
                        return BadRequest("Invalid image id for selected source and id");
                    if (fanart.Enabled != 1)
                        return BadRequest("Image is disabled");
                    break;
                }
                default:
                    return BadRequest("Invalid source");
            }

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.Fanart) ?? new() { AnimeID = series.AniDB_ID };

            defaultImage.ImageParentID = imageID;
            defaultImage.ImageParentType = (int) imageEntityType.Value;
            defaultImage.ImageType = (int) ImageSizeType.Fanart;
            RepoFactory.AniDB_Anime_DefaultImage.Save(defaultImage);
            return new Image(imageID, imageEntityType.Value, true);
        }

        /// <summary>
        /// Unset the default fanart for the series.
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns></returns>
        [HttpDelete("{seriesID}/Images/Fanart")]
        public ActionResult DeleteSeriesDefaultFanart([FromRoute] int seriesID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return NotFound("No Series with ID");
            if (!User.AllowedSeries(series)) return Forbid("Series not allowed for current user");

            var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(series.AniDB_ID, ImageSizeType.Fanart);
            if (defaultImage == null)
                return BadRequest("No default Fanart.");

            RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
            
            // Update the contract data (used by Shoko Desktop).
            RepoFactory.AnimeSeries.Save(series, false);
            return NoContent();
        }

        /// <summary>
        /// Get tags for Series with ID, optionally applying the given <see cref="TagFilter.Filter" />
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="filter"></param>
        /// <param name="excludeDescriptions"></param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Tags")]
        public ActionResult<List<Tag>> GetSeriesTags([FromRoute] int seriesID, [FromQuery] TagFilter.Filter filter = 0, [FromQuery] bool excludeDescriptions = false)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.GetTags(HttpContext, anime, filter, excludeDescriptions);
        }

        /// <summary>
        /// Get tags for Series with ID, applying the given TagFilter (0 is show all)
        /// </summary>
        ///
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="filter"></param>
        /// <param name="excludeDescriptions"></param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Tags/{filter}")]
        [Obsolete]
        public ActionResult<List<Tag>> GetSeriesTagsFromPath([FromRoute] int seriesID, [FromRoute] TagFilter.Filter filter, [FromQuery] bool excludeDescriptions = false)
            => GetSeriesTags(seriesID, filter, excludeDescriptions);

        /// <summary>
        /// Get the cast listing for series with ID
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="roleType">Filter by role type</param>
        /// <returns></returns>
        [HttpGet("{seriesID}/Cast")]
        public ActionResult<List<Role>> GetSeriesCast([FromRoute] int seriesID, [FromQuery] Role.CreatorRoleType? roleType = null)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return Series.GetCast(HttpContext, ser.AniDB_ID, roleType);
        }

        /// <summary>
        /// Move the series to a new group, and update accordingly
        /// </summary>
        /// <param name="seriesID">Shoko ID</param>
        /// <param name="newGroupID"></param>
        /// <returns></returns>
        [HttpPatch("{seriesID}/Move/{newGroupID}")]
        public ActionResult MoveSeries([FromRoute] int seriesID, int newGroupID)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return BadRequest("No Series with ID");
            var grp = RepoFactory.AnimeGroup.GetByID(newGroupID);
            if (grp == null) return BadRequest("No Group with ID");
            series.MoveSeries(grp);
            return Ok();
        }

        /// <summary>
        /// Delete a Series
        /// </summary>
        /// <param name="seriesID">The ID of the Series</param>
        /// <param name="deleteFiles">Whether to delete all of the files in the series from the disk.</param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{seriesID}")]
        public ActionResult DeleteSeries([FromRoute] int seriesID, [FromQuery] bool deleteFiles = false)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series == null) return BadRequest("No Series with ID");

            series.DeleteSeries(deleteFiles, true);

            return Ok();
        }

        #region Search

        /// <summary>
        /// Search for series with given query in name or tag
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="fuzzy">whether or not to use fuzzy search</param>
        /// <param name="limit">number of return items</param>
        /// <returns>List<see cref="SeriesSearchResult"/></returns>
        [HttpGet("Search/{query}")]
        public ActionResult<IEnumerable<SeriesSearchResult>> Search([FromRoute] string query, [FromQuery] bool fuzzy = true, [FromQuery] int limit = int.MaxValue)
        {
            SorensenDice search = new SorensenDice();
            query = query.ToLowerInvariant();
            query = query.Replace("+", " ");

            List<SeriesSearchResult> seriesList = new List<SeriesSearchResult>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(User.GetHideCategories()))
                .AsParallel();

            HashSet<string> languages = new HashSet<string>{"en", "x-jat"};
            languages.UnionWith(ServerSettings.Instance.LanguagePreference);
            var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<double, string>>();

            if (fuzzy)
                allSeries.ForAll(a => CheckTitlesFuzzy(search, languages, a, query, ref distLevenshtein, limit));
            else
                allSeries.ForAll(a => CheckTitles(languages, a, query, ref distLevenshtein, limit));

            var tempListToSort = distLevenshtein.Keys.GroupBy(a => a.AnimeGroupID).Select(a =>
            {
                var tempSeries = a.ToList();
                tempSeries.Sort((j, k) =>
                {
                    var result1 = distLevenshtein[j];
                    var result2 = distLevenshtein[k];
                    var exactMatch = result1.Item1.CompareTo(result2.Item1);
                    if (exactMatch != 0) return exactMatch;

                    string title1 = j.GetSeriesName();
                    string title2 = k.GetSeriesName();
                    if (title1 == null && title2 == null) return 0;
                    if (title1 == null) return 1;
                    if (title2 == null) return -1;
                    return string.Compare(title1, title2, StringComparison.InvariantCultureIgnoreCase);
                });
                var result = new SearchGrouping
                {
                    Series = a.OrderBy(b => b.AirDate).ToList(),
                    Distance = distLevenshtein[tempSeries[0]].Item1,
                    Match = distLevenshtein[tempSeries[0]].Item2
                };
                return result;
            });

            Dictionary<SVR_AnimeSeries, Tuple<double, string>> series = tempListToSort.OrderBy(a => a.Distance)
                .ThenBy(a => a.Match.Length).SelectMany(a => a.Series).ToDictionary(a => a, a => distLevenshtein[a]);
            foreach (KeyValuePair<SVR_AnimeSeries, Tuple<double, string>> ser in series)
            {
                seriesList.Add(new SeriesSearchResult(HttpContext, ser.Key, ser.Value.Item2, ser.Value.Item1));
                if (seriesList.Count >= limit)
                {
                    break;
                }
            }

            return seriesList;
        }
        
        /// <summary>
        /// Search the title dump for the given query or directly using the anidb id.
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="includeTitles">Include titles</param>
        /// <returns></returns>
        [HttpGet("AniDB/Search/{query}")]
        public List<Series.AniDBSearchResult> OnlineAnimeTitleSearch([FromRoute] string query, [FromQuery] bool includeTitles = true)
        {
            List<Series.AniDBSearchResult> list = new List<Series.AniDBSearchResult>();

            // check if it is a title search or an ID search
            if (int.TryParse(query, out int aid))
            {
                // user is direct entering the anime id

                // try the local database first
                // if not download the data from AniDB now
                SVR_AniDB_Anime anime = Server.ShokoService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                    ServerSettings.Instance.AniDb.DownloadRelatedAnime);
                if (anime != null)
                {
                    Series.AniDBSearchResult res = new Series.AniDBSearchResult
                    {
                        ID = anime.AnimeID,
                        Title = anime.MainTitle,
                        Titles = includeTitles ? anime.GetTitles().Select(title => new Title
                            {
                                Language = title.Language,
                                Name = title.Title,
                                Type = title.TitleType,
                                Default = string.Equals(title.Title, anime.MainTitle),
                                Source = "AniDB",
                            }
                        ).ToList() : null,
                    };

                    // check for existing series and group details
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                    if (ser != null)
                    {
                        res.ShokoID = ser.AnimeSeriesID;
                        res.Title = anime.GetFormattedTitle();
                    }
                    list.Add(res);
                }
            }
            else
            {
                // title search so look at the web cache
                var result = AniDB_TitleHelper.Instance.SearchTitle(HttpUtility.UrlDecode(query));

                foreach (var item in result)
                {
                    var mainTitle = (item.Titles.FirstOrDefault(a => a.TitleLanguage == "x-jat" && a.TitleType == "main") ?? item.Titles.FirstOrDefault());
                    Series.AniDBSearchResult res = new Series.AniDBSearchResult
                    {
                        ID = item.AnimeID,
                        Title = mainTitle.Title,
                        Titles = includeTitles ? item.Titles.Select(title => new Title
                            {
                                Language = title.TitleLanguage,
                                Name = title.Title,
                                Type = title.TitleType,
                                Default = title == mainTitle,
                                Source = "AniDB",
                            }
                        ).ToList() : null,
                    };

                    // check for existing series and group details
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(item.AnimeID);
                    if (ser != null)
                    {
                        res.ShokoID = ser.AnimeSeriesID;
                        res.Title = ser.GetAnime().GetFormattedTitle();
                    }

                    list.Add(res);
                }
            }

            return list;
        }

        /// <summary>
        /// Searches for series whose title starts with a string
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        [HttpGet("StartsWith/{query}")]
        public ActionResult<List<SeriesSearchResult>> StartsWith([FromRoute] string query, int limit = int.MaxValue)
        {
            query = query.ToLowerInvariant();

            List<SeriesSearchResult> seriesList = new List<SeriesSearchResult>();
            ConcurrentDictionary<SVR_AnimeSeries, string> tempSeries = new ConcurrentDictionary<SVR_AnimeSeries, string>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(User.GetHideCategories()))
                .AsParallel();

            #region Search_TitlesOnly
            allSeries.ForAll(a => CheckTitlesStartsWith(a, query, ref tempSeries, limit));
            Dictionary<SVR_AnimeSeries, string> series =
                tempSeries.OrderBy(a => a.Value).ToDictionary(a => a.Key, a => a.Value);

            foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
            {
                seriesList.Add(new SeriesSearchResult(HttpContext, ser.Key, ser.Value, 0));
                if (seriesList.Count >= limit)
                {
                    break;
                }
            }

            #endregion

            return seriesList;
        }

        /// <summary>
        /// Get the series that reside in the path that ends with <param name="path"></param>
        /// </summary>
        /// <returns></returns>
        [HttpGet("PathEndsWith/{*path}")]
        public ActionResult<List<Series>> GetSeries([FromRoute] string path)
        {
            var query = path;
            if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
            if (query.Contains("%")) query = Uri.UnescapeDataString(query);
            query = query.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            // There should be no circumstance where FullServerPath has no Directory Name, unless you have missing import folders
            return RepoFactory.VideoLocalPlace.GetAll().AsParallel()
                .Where(a => a.FullServerPath != null && Path.GetDirectoryName(a.FullServerPath)
                    .EndsWith(query, StringComparison.OrdinalIgnoreCase))
                .SelectMany(a => a.VideoLocal.GetAnimeEpisodes()).Select(a => a.GetAnimeSeries())
                .Distinct()
                .Where(ser => ser == null || User.AllowedSeries(ser)).Select(a => new Series(HttpContext, a)).ToList();
        }

        #region internal function

        /// <summary>
        /// function used in fuzzy search
        /// </summary>
        /// <param name="search"></param>
        /// <param name="languages"></param>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        [NonAction]
        private static void CheckTitlesFuzzy(IStringDistance search, HashSet<string> languages, SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, Tuple<double, string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AnimeTitles == null) return;
            var dist = double.MaxValue;
            string match = string.Empty;

            var seriesTitles = a.Contract.AniDBAnime.AnimeTitles
                .Where(b => languages.Contains(b.Language.ToLower()) &&
                            b.TitleType != Shoko.Models.Constants.AnimeTitleType.ShortName).Select(b => b.Title)
                .ToList();
            foreach (string title in seriesTitles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                var result = 0.0;
                // Check for exact match
                if (!title.Equals(query, StringComparison.Ordinal))
                    result = search.Distance(title, query);
                // For Dice, 1 is no reasonable match
                if (result >= 1) continue;
                // Don't count an error as liberally when the title is short
                if (title.Length < 5 && result > 0.8) continue;
                if (result < dist)
                {
                    match = title;
                    dist = result;
                } else if (Math.Abs(result - dist) < 0.00001)
                {
                    if (title.Length < match.Length) match = title;
                }
            }
            // Keep the lowest distance, then by shortest title
            if (dist < double.MaxValue)
                distLevenshtein.AddOrUpdate(a, new Tuple<double, string>(dist, match),
                    (key, oldValue) =>
                    {
                        if (oldValue.Item1 < dist) return oldValue;
                        if (Math.Abs(oldValue.Item1 - dist) < 0.00001)
                            return oldValue.Item2.Length < match.Length
                                ? oldValue
                                : new Tuple<double, string>(dist, match);

                        return new Tuple<double, string>(dist, match);
                    });
        }

        /// <summary>
        /// function used in search
        /// </summary>
        /// <param name="languages"></param>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        [NonAction]
        private static void CheckTitles(HashSet<string> languages, SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, Tuple<double, string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AnimeTitles == null) return;
            var dist = double.MaxValue;
            string match = string.Empty;

            var seriesTitles = a.Contract.AniDBAnime.AnimeTitles
                .Where(b => languages.Contains(b.Language.ToLower()) &&
                            b.TitleType != Shoko.Models.Constants.AnimeTitleType.ShortName).Select(b => b.Title)
                .ToList();
            foreach (string title in seriesTitles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                var result = 1.0;
                // Check for exact match
                if (title.Equals(query, StringComparison.Ordinal))
                {
                    result = 0.0;
                }
                else
                {
                    var index = title.IndexOf(query, StringComparison.InvariantCultureIgnoreCase);
                    if (index >= 0) result = ((double) title.Length - index) / title.Length * 0.8D; // ensure that 0.8 doesn't skip later
                }
                // For Dice, 1 is no reasonable match
                if (result >= 1) continue;
                // Don't count an error as liberally when the title is short
                if (title.Length < 5 && result > 0.8) continue;
                if (result < dist)
                {
                    match = title;
                    dist = result;
                } else if (Math.Abs(result - dist) < 0.00001)
                {
                    if (title.Length < match.Length) match = title;
                }
            }
            // Keep the lowest distance, then by shortest title
            if (dist < double.MaxValue)
                distLevenshtein.AddOrUpdate(a, new Tuple<double, string>(dist, match),
                    (key, oldValue) =>
                    {
                        if (oldValue.Item1 < dist) return oldValue;
                        if (Math.Abs(oldValue.Item1 - dist) < 0.00001)
                            return oldValue.Item2.Length < match.Length
                                ? oldValue
                                : new Tuple<double, string>(dist, match);

                        return new Tuple<double, string>(dist, match);
                    });
        }

        class SearchGrouping
        {
            public List<SVR_AnimeSeries> Series { get; set; }
            public double Distance { get; set; }
            public string Match { get; set; }
        }

        [NonAction]
        private static void CheckTitlesStartsWith(SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, string> series, int limit)
        {
            if (series.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return;
            string match = string.Empty;
            foreach (string title in a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title).ToList())
            {
                if (string.IsNullOrEmpty(title)) continue;
                if (title.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                {
                    match = title;
                }
            }
            // Keep the lowest distance
            if (match != string.Empty)
                series.TryAdd(a, match);
        }

        #endregion
        #endregion
    }
}
