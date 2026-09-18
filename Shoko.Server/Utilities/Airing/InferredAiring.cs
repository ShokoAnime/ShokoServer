using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// An episode airing to store, as the delay inference resolved it.
/// </summary>
public sealed record InferredAiring
{
    /// <summary>
    /// The airing's key, unique within its schedule.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The key of the episode the airing is for.
    /// </summary>
    public required string EpisodeKey { get; init; }

    /// <summary>
    /// The key of the existing airing this one updates, or <see langword="null"/>
    /// when it is a new row. It differs from <see cref="Key"/> when the provider
    /// re-keyed an airing, which keeps the row, and so its local ID and links.
    /// </summary>
    public string? ExistingKey { get; init; }

    /// <summary>
    /// The resolved slot, in UTC, or <see langword="null"/> when the airing has
    /// no slot.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// The first slot the airing was scheduled for, in UTC, or
    /// <see langword="null"/> when it never moved.
    /// </summary>
    public DateTime? OriginalAiredAt { get; init; }

    /// <summary>
    /// Whether this airing's own slot was postponed.
    /// </summary>
    public bool IsDelayed { get; init; }

    /// <summary>
    /// The key of the link head this airing belongs to, or <see langword="null"/>
    /// when it isn't linked, or was unlinked for having drifted from its head.
    /// </summary>
    public string? LinkKey { get; init; }

    /// <summary>
    /// Whether the write took the airing off the schedule's listing and kept it
    /// without a slot, rather than storing it as it was submitted. A hiatus is
    /// written like any other row, so this is what tells the two apart after
    /// the fact.
    /// </summary>
    public bool IsWithdrawn { get; init; }

    /// <summary>
    /// Whether the airing is a new row rather than an update of an existing one.
    /// </summary>
    public bool IsNew => ExistingKey is null;

    /// <summary>
    /// Whether the airing kept an existing row under a new key.
    /// </summary>
    public bool IsReKeyed => ExistingKey is not null && !string.Equals(ExistingKey, Key, StringComparison.Ordinal);
}
