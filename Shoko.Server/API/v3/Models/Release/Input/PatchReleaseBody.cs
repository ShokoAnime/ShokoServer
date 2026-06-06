namespace Shoko.Server.API.v3.Models.Release.Input;

/// <summary>
/// Fields that can be patched on a stored release via the API.
/// Omitted (null) fields leave the existing value unchanged.
/// </summary>
public class PatchReleaseBody
{
    /// <summary>
    /// Whether the release is publicly available. Set to <c>false</c> for
    /// custom or private releases to prevent AniDB from scanning them.
    /// </summary>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// When set to <c>true</c>, no provider will attempt to rescan this
    /// release regardless of the backoff schedule.
    /// </summary>
    public bool? PreventRescan { get; init; }
}
