
namespace Shoko.Abstractions.Core;

/// <summary>
/// The release channel of the currently running server.
/// </summary>
public enum ReleaseChannel
{
    /// <summary>
    /// The debug channel.
    /// </summary>
    Debug = 0,

    /// <summary>
    /// The stable channel.
    /// </summary>
    Stable = 1,

    /// <summary>
    /// The dev channel.
    /// </summary>
    Dev = 2,
}
