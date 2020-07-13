using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    /// <summary>
    /// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
    /// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}"), ApiV3]
    [Authorize]
    public class TreeController : BaseController
    {
        
        /// <summary>
        /// Get All Filters
        /// </summary>
        /// <returns></returns>
        [HttpGet("Filter")]
        public ActionResult<List<Filter>> GetFilters(bool includeEmpty = false, bool includeInvisible = false)
        {
            var fs = RepoFactory.GroupFilter.GetTopLevel();
            return fs.Where(a =>
                {
                    if (a.InvisibleInClients != 0 && !includeInvisible) return false;
                    if (a.GroupsIds.ContainsKey(User.JMMUserID) && a.GroupsIds[User.JMMUserID].Count > 0 || includeEmpty)
                        return true;
                    return ((GroupFilterType) a.FilterType).HasFlag(GroupFilterType.Directory);
                })
                .Select(a => new Filter(HttpContext, a)).OrderBy(a => a.Name).ToList();
        }
        
        /// <summary>
        /// Get groups for filter with ID
        /// </summary>
        /// <returns></returns>
        [HttpGet("Filter/{filterID}/Group")]
        public ActionResult<List<Group>> GetGroups(int filterID)
        {
            var f = RepoFactory.GroupFilter.GetByID(filterID);
            if (f == null) return BadRequest("No Filter with ID");
            if (!f.GroupsIds.ContainsKey(User.JMMUserID)) return new List<Group>();
            return f.GroupsIds[User.JMMUserID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                .Where(a => a != null).GroupFilterSort(f).Select(a => new Group(HttpContext, a)).ToList();
        }
        
        /// <summary>
        /// Get series for group with ID. Pass a <see cref="filterID"/> of 0 to apply no filtering
        /// </summary>
        /// <returns></returns>
        [HttpGet("Filter/{filterID}/Group/{groupID}/Series")]
        public ActionResult<List<Series>> GetSeries(int filterID, int groupID)
        {
            var grp = RepoFactory.AnimeGroup.GetByID(groupID);
            if (grp == null) return BadRequest("No Group with ID");
            if (filterID == 0)
                return grp.GetSeries().Where(a => User.AllowedSeries(a)).Select(a => new Series(HttpContext, a))
                    .ToList();

            var f = RepoFactory.GroupFilter.GetByID(filterID);
            if (f == null) return BadRequest("No Filter with ID");

            if (f.ApplyToSeries != 1)
                return grp.GetSeries().Where(a => User.AllowedSeries(a)).Select(a => new Series(HttpContext, a))
                    .ToList();

            if (!f.SeriesIds.ContainsKey(User.JMMUserID)) return new List<Series>();

            return f.SeriesIds[User.JMMUserID].Select(id => RepoFactory.AnimeSeries.GetByID(id))
                .Where(ser => ser?.AnimeGroupID == groupID).Select(ser => new Series(HttpContext, ser)).OrderBy(a =>
                    Series.GetAniDBInfo(HttpContext, RepoFactory.AniDB_Anime.GetByAnimeID(a.IDs.ID)).AirDate)
                .ToList();
        }
        
        /// <summary>
        /// Get Episodes for Series with seriesID. Filter or group info is irrelevant at this level
        /// </summary>
        /// <returns></returns>
        [HttpGet("Series/{seriesID}/Episode")]
        public ActionResult<List<Episode>> GetEpisodes(int seriesID, bool includeMissing = false)
        {
            var ser = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (ser == null) return BadRequest("No Series with ID");
            if (!User.AllowedSeries(ser)) return BadRequest("Series not allowed for current user");
            return ser.GetAnimeEpisodes().Select(a => new Episode(HttpContext, a))
                .Where(a => a.Size > 0 || includeMissing).ToList();
        }
    }
    
    
}