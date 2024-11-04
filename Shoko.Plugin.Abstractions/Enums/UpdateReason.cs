
namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Reason for an metadata update event to be dispatched.
/// </summary>
public enum UpdateReason
{
    /// <summary>
    /// Nothing occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The metadata was added.
    /// </summary>
    Added = 1,

    /// <summary>
    /// The metadata was updated.
    /// </summary>
    Updated = 2,

    /// <summary>
    /// The metadata was removed.
    /// </summary>
    Removed = 4,
}
