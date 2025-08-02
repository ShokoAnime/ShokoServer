using System;

namespace Shoko.Plugin.Abstractions.Exceptions;

/// <summary>
/// Indicates that the AniDB user has been temporarily (or permanently) banned.
/// </summary>
public class AnidbHttpBannedException(Exception? innerException = null) : Exception("Got an AniDB HTTP ban!", innerException)
{
    /// <summary>
    /// When we assume the ban has expired and will allow trying again.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
}
