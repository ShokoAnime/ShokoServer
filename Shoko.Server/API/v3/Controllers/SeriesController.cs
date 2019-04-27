using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
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
            var allSeries = RepoFactory.AnimeSeries.GetAll().Where(a => User.AllowedSeries(a)).ToList();
            return allSeries.Select(a => new Series(HttpContext, a.AnimeSeriesID)).ToList();
        }

        /// <summary>
        /// Get the series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public ActionResult<Series> GetSeries(int id)
        {
            return new Series(HttpContext, id);
        }

        /// <summary>
        /// Get AniDB Info for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/AniDB")]
        public ActionResult<Series.AniDB> GetSeriesAniDBDetails(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.GetAniDBInfo(HttpContext, anime);
        }
        
        /// <summary>
        /// Get TvDB Info for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/TvDB")]
        public ActionResult<List<Series.TvDB>> GetSeriesTvDBDetails(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetTvDBInfo(HttpContext, ser);
        }
        
        /// <summary>
        /// Get all images for series with ID, optionally with Disabled images, as well.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includeDisabled"></param>
        /// <returns></returns>
        [HttpGet("{id}/Images/{IncludeDisabled?}")]
        public ActionResult<Images> GetSeriesImages(int id, bool includeDisabled)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetArt(HttpContext, ser.AniDB_ID, includeDisabled);
        }
        
        /// <summary>
        /// Get tags for Series with ID, applying the given TagFilter (0 is show all)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpGet("{id}/Tags/{filter}")]
        public ActionResult<List<Tag>> GetSeriesTags(int id, TagFilter.Filter filter)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            var anime = ser.GetAnime();
            if (anime == null) return BadRequest("No AniDB_Anime for Series");
            return Series.GetTags(HttpContext, anime, filter);
        }
        
        /// <summary>
        /// Get the cast listing for series with ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/Cast")]
        public ActionResult<List<Role>> GetSeriesCast(int id)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return BadRequest("No Series with ID");
            return Series.GetCast(HttpContext, ser.AniDB_ID);
        }
    }
}
