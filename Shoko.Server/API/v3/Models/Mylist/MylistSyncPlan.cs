using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;

namespace Shoko.Server.API.v3.Models.Mylist;

/// <summary>
/// What a MyList sync worked out that it should do. A plan-only run returns one
/// having done none of it, so it can be shown, narrowed, and sent back to
/// <c>Sync/Plan/Apply</c>.
/// </summary>
public class MylistSyncPlan
{
    /// <summary>
    /// When the plan was worked out, in UTC. Output only, and informational:
    /// nothing refuses a plan for being old. A client can show its age and
    /// offer to work out a fresh one, but applying an old plan is safe — no
    /// values cross the wire, so every one is taken from current state when the
    /// step runs.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The steps to take, in the order the sync arrived at them.
    /// </summary>
    [Required]
    public List<MylistSyncAction> Actions { get; set; } = [];

    public MylistSyncPlan() { }

    public MylistSyncPlan(Abstractions.Metadata.Anidb.Models.MylistSyncPlan plan, List<MylistSyncAction> actions)
    {
        CreatedAt = plan.CreatedAt;
        Actions = actions;
    }
}

/// <summary>
/// One step of a plan. Carries the entities it acts on so a client can render
/// the plan without looking each one up, and their ids so the same body can be
/// posted back to apply it.
///
/// Only the kind and the ids are read on input; the rest is worked out from
/// current state when the step runs. A caller therefore chooses which entries
/// to act on and what to do with them, but never what values get written.
/// </summary>
public class MylistSyncAction
{
    /// <summary>
    /// What this step does.
    /// </summary>
    [Required]
    public MylistSyncActionKind Kind { get; set; }

    /// <summary>
    /// Which side the step writes to. <c>None</c> means it would change
    /// nothing.
    /// </summary>
    [Required]
    public MylistSyncDirection Direction { get; set; }

    /// <summary>
    /// A short line describing the step.
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The id of the local file the step concerns. One of this or
    /// <see cref="AnidbEpisodeID"/> is required on input.
    /// </summary>
    public int? FileID { get; set; }

    /// <summary>
    /// The AniDB episode id the step concerns. Resolves both the AniDB episode
    /// and the local one, so it is the only episode id the input needs. One of
    /// this or <see cref="FileID"/> is required on input.
    /// </summary>
    public int? AnidbEpisodeID { get; set; }

    /// <summary>
    /// The list id (lid) of the entry the step acts on, when AniDB already
    /// holds one. Optional: an entry can equally be addressed by the file's
    /// hash and size, or by anime, episode type and number, both of which come
    /// from the ids above.
    /// </summary>
    public ulong? MylistID { get; set; }

    /// <summary>
    /// The local file, when the step concerns one. Output only; apply reads
    /// <see cref="FileID"/> instead, so a client can post this body back
    /// unchanged.
    /// </summary>
    public Shoko.File? File { get; set; }

    /// <summary>
    /// The local episode, when the step concerns one and the collection has it.
    /// Output only, as <see cref="File"/>.
    /// </summary>
    public Shoko.Episode? Episode { get; set; }

    /// <summary>
    /// The local watched state for the file, when the step concerns one. The
    /// local side of the comparison that produced the step, as
    /// <see cref="Entry"/> is the AniDB side. Output only.
    /// </summary>
    public Shoko.File.FileUserData? FileUserData { get; set; }

    /// <summary>
    /// The local watched state for the episode, when the step concerns one.
    /// The local side of the comparison, as <see cref="Entry"/> is the AniDB
    /// side. Output only.
    /// </summary>
    public Shoko.Episode.EpisodeUserData? EpisodeUserData { get; set; }

    /// <summary>
    /// The MyList entry as it stands, or null when the step is what creates it.
    /// Output only, as <see cref="File"/>.
    /// </summary>
    public MylistEntry? Entry { get; set; }
}
