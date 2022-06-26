using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class GroupController : BaseController
    {
        #region Return messages

        internal static string GroupNotFound = "No Group entry for the given groupID";

        internal static string GroupForbiddenForUser = "Accessing Group is not allowed for the current user";

        #endregion
        #region Metadata
        #region Get Many
        /// <summary>
        /// Get a list of all groups available to the current user
        /// </summary>
        /// <param name="page">The page index.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="topLevelOnly">Only list the top level groups if set.</param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<Group>> GetAllGroups([FromQuery] int page = 0, [FromQuery] int pageSize = 50, bool topLevelOnly = false)
        {
            var groups = RepoFactory.AnimeGroup.GetAll()
                .Where(group =>
                {
                    if (topLevelOnly && group.AnimeGroupParentID.HasValue)
                        return false;

                    return User.AllowedGroup(group);
                })
                .OrderBy(group => group.GroupName);

            if (pageSize <= 0)
                return groups
                    .Select(g => new Group(HttpContext, g))
                    .ToList();

            if (page <= 0) page = 0;
            return groups
                .Skip(page * pageSize)
                .Take(pageSize)
                .Select(g => new Group(HttpContext, g))
                .ToList();
        }

        #endregion
        #region Create new or update existing

        /// <summary>
        /// Create a new or merge with an existing group.
        /// Use <see cref="SeriesController.MoveSeries"/> to move series to the group.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="merge">Merge with the first existing group of the same name.</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult<Group> CreateOrUpdateGroup([FromBody] Group.Input.CreateGroupBody body, [FromQuery] bool merge = false)
        {
            // Validate if the parent exists if a parent id is set.
            var parentID = body.ParentID ?? 0;
            if (parentID != 0)
            {
                var parent = RepoFactory.AnimeGroup.GetByID(parentID);
                if (parent == null)
                    return BadRequest("Invalid parent group id supplied.");
            }

            // Try to find the group to merge with if we provided an id.
            bool isNew = false;
            SVR_AnimeGroup group = null;
            if (body.ID.HasValue && body.ID.Value != 0)
            {
                // Make sure merging is requested.
                // A merge request without the 'merge' query parameter set is an
                // error.
                if (!merge)
                    return BadRequest("A group id have been supplied. Set the merge query parameter to continue.");

                group = RepoFactory.AnimeGroup.GetByID(body.ID.Value);

                // Make sure the group actually exists.
                if (group == null)
                    return BadRequest("No Group entry for the given id.");
            }
            // Try to find an existing group exists for the given name or create a new one.
            else
            {
                // Look for an existing group if force is not set.
                group = RepoFactory.AnimeGroup.GetByParentID(parentID)
                    .FirstOrDefault(grp => string.Equals(grp.GroupName, body.Name, StringComparison.InvariantCultureIgnoreCase));

                // If no group was found (either because we forced it or because a group by the same name was not found) then
                if (group == null)
                {
                    // It's safe to use the group name.
                    isNew = true;
                    group = new()
                    {
                        Description = string.Empty,
                        IsManuallyNamed = 0,
                        DateTimeCreated = DateTime.Now,
                        DateTimeUpdated = DateTime.Now,
                        MissingEpisodeCount = 0,
                        MissingEpisodeCountGroups = 0,
                        OverrideDescription = 0,

                    };
                }
                // Else we need to provide the query parameter to either merge
                // or forcefully create a new group.
                else if (!merge)
                {
                    return BadRequest("A group with the given name already exists. Set the merge or force query parameter to continue.");
                }
            }

            // Get the series and validate the series ids.
            var seriesList = body.SeriesIDs?.Select(id => RepoFactory.AnimeSeries.GetByID(id)).Where(s => s != null).ToList() ?? new();
            if (seriesList.Count != (body.SeriesIDs?.Length ?? 0))
                return BadRequest("One or more series ids are invalid.");
            // Trying to merge 0 series with an existing group is an error.
            // Trying to create an empty group is also an error.
            if (seriesList.Count == 0)
                return BadRequest("No series ids have been spesified.");

            // Nor spesifying a group name is an error.
            body.Name = body.Name?.Trim() ?? "";
            if (string.IsNullOrEmpty(body.Name))
                return BadRequest("A name must be present and contain at least one character.");

            group.AnimeGroupParentID = parentID != 0 ? parentID : null;
            group.GroupName = body.Name;
            group.SortName = string.IsNullOrEmpty(body.SortName?.Trim()) ? body.Name : body.SortName;
            group.Description = body.Description ?? null;
            group.IsManuallyNamed = body.HasCustomName ? 1 : 0;
            group.OverrideDescription = 0;

            // Create a new or update an existing group entry.
            RepoFactory.AnimeGroup.Save(group, true, false, false);

            // Iterate over the series list to calculate the groups to update
            // and to assign the series to the group.
            var oldGroups = new Dictionary<int, int>();
            foreach (var series in seriesList)
            {
                // Skip the series if it's already in the group.
                if (series.AnimeGroupID == group.AnimeGroupID)
                    continue;

                // Count the number of series in each group.
                var oldGroupID = series.AnimeGroupID;
                if (oldGroups.TryGetValue(oldGroupID, out var count))
                    oldGroups[oldGroupID] = count + 1;
                else
                    oldGroups[oldGroupID] = 1;

                // Assign the series to the new group and update the series
                // entry.
                series.AnimeGroupID = group.AnimeGroupID;
                series.UpdateStats(true, true, false);
            }

            // We don't need to update the group twice, and we don't need to
            // check if the group we're creating/updating needs to be
            // removed.
            if (oldGroups.Keys.Contains(group.AnimeGroupID))
                oldGroups.Remove(group.AnimeGroupID);

            // Remove or update groups stats for the old groups.
            foreach (var (oldGroupID, seriesCount) in oldGroups)
            {
                var oldGroup = RepoFactory.AnimeGroup.GetByID(oldGroupID)?.TopLevelAnimeGroup;
                // The old group may have already been removed, so silently skip it.
                if (oldGroup == null)
                    continue;
                // Delete the sub-groups if the old group doesn't contain any other series.
                if (oldGroup.GetAllSeries().Count <= seriesCount)
                {
                    RepoFactory.AnimeGroup.Delete(oldGroup.GetAllChildGroups());
                    RepoFactory.AnimeGroup.Delete(oldGroup);
                }
                else
                {
                    oldGroup.UpdateStatsFromTopLevel(false, true, true);
                }
            }

            // Update the group stats for the new group.
            group.UpdateStatsFromTopLevel(false, true, true);

            // Return the info for the newly created or updated group.
            var dto = new Group(HttpContext, group);
            return isNew ? Created($"./{group.AnimeGroupID}", dto) : Ok(dto);
        }

        #endregion
        #region Get One

        /// <summary>
        /// Get the group with ID
        /// </summary>
        /// <param name="groupID"></param>
        /// <returns></returns>
        [HttpGet("{groupID}")]
        public ActionResult<Group> GetGroup([FromRoute] int groupID)
        {
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupNotFound);

            return new Group(HttpContext, group);
        }

        #endregion
        #endregion
        #region Delete

        /// <summary>
        /// Delete a group recursively.
        /// </summary>
        /// <param name="groupID">The ID of the group to delete</param>
        /// <param name="deleteSeries">Whether to delete the series in the group. It will error if this is false and the group is not empty.</param>
        /// <param name="deleteFiles">Whether to delete the all of the files in the group from the disk.</param>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpDelete("{groupID}")]
        public ActionResult DeleteGroup(int groupID, bool deleteSeries = false, bool deleteFiles = false)
        {
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound("No Group with ID");

            if (!deleteSeries && group.GetAllSeries().Any())
                return BadRequest($"{nameof(deleteSeries)} is not true, and the group contains series. Move them, or set {nameof(deleteSeries)} to true");

            foreach (var series in group.GetAllSeries())
                series.DeleteSeries(deleteFiles, false);

            group.DeleteGroup();

            return NoContent();
        }

        #endregion
        #region Recalculate

        /// <summary>
        /// Recalculate all stats and contracts for a group
        /// </summary>
        /// <param name="groupID"></param>
        /// <returns></returns>
        [HttpPost("{groupID}/Recalculate")]
        public ActionResult RecalculateStats(int groupID)
        {
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null)
                return NotFound(GroupNotFound);

            AnimeGroupCreator groupCreator = new AnimeGroupCreator();
            groupCreator.RecalculateStatsContractsForGroup(group);
            return Ok();
        }

        #endregion
        #region Obsolete

        /// <summary>
        /// Recreate all groups from scratch. Use <see cref="ActionController.RecreateAllGroups"/> instead.
        /// </summary>
        /// <returns></returns>
        [Authorize("admin")]
        [HttpGet("RecreateAllGroups")]
        [Obsolete]
        public ActionResult RecreateAllGroups()
        {
            Task.Run(() => new AnimeGroupCreator().RecreateAllGroups());
            return Ok("Check the server status via init/status or SignalR's Events hub");
        }

        #endregion
    }
}
