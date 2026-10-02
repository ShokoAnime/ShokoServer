using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;

namespace Shoko.Server.Actions;

/// <summary>
///   Marks a group action that only touches the series the current actor may
///   see, so a restricted user may run it on any group visible to them. Every
///   other group action needs the whole group to be visible.
/// </summary>
public interface IVisibleSeriesGroupAction;

/// <summary>
///   Helpers for <see cref="IVisibleSeriesGroupAction"/>.
/// </summary>
public static class VisibleSeriesGroupActionExtensions
{
    /// <summary>
    ///   The series of a group, at any level, the current actor may see, or
    ///   every series when the action runs for the system.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="actorContext">The current actor.</param>
    /// <returns>The series.</returns>
    public static IReadOnlyList<AnimeSeries> GetVisibleSeries(this IShokoGroup group, IActorContext actorContext)
        => AnimeGroupView.For((AnimeGroup)group, actorContext.Current?.User).AllSeries;
}
