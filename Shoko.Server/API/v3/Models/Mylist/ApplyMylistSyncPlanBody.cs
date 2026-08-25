using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Server.API.v3.Models.Mylist;

/// <summary>
/// A plan to carry out. Deliberately a subset of <see cref="MylistSyncPlan"/>,
/// sharing its property names, so the body returned by a plan endpoint can be
/// posted back as-is with the steps the caller does not want removed — the
/// fields it does not read are simply ignored.
/// </summary>
public class ApplyMylistSyncPlanBody
{
    /// <summary>
    /// The steps to carry out.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<ApplyMylistSyncActionBody> Actions { get; set; } = [];
}

/// <summary>
/// One step to carry out: what to do, and what to do it to.
///
/// Nothing here says what gets written. The values a step writes are worked out
/// from current state when it runs, so a caller chooses which entries to act on
/// and what to do with them, but never the data that lands on AniDB.
/// </summary>
public class ApplyMylistSyncActionBody
{
    /// <summary>
    /// What this step does.
    /// </summary>
    [Required]
    public MylistSyncActionKind Kind { get; set; }

    /// <summary>
    /// The local file the step concerns. One of this or
    /// <see cref="AnidbEpisodeID"/> is required.
    /// </summary>
    public int? FileID { get; set; }

    /// <summary>
    /// The AniDB episode the step concerns. Resolves both the AniDB episode and
    /// the local one, so it is the only episode id needed. One of this or
    /// <see cref="FileID"/> is required.
    /// </summary>
    public int? AnidbEpisodeID { get; set; }

    /// <summary>
    /// The list id (lid) of the entry to act on, when AniDB already holds one.
    /// Optional: an entry can equally be addressed by the file's hash and size,
    /// or by anime, episode type and number, both of which come from the ids
    /// above.
    /// </summary>
    public ulong? MylistID { get; set; }
}
