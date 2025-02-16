using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Models.Release;

/// <summary>
/// Information about an attempt to match a video to a release.
/// </summary>
public class StoredReleaseInfo_MatchAttempt
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int StoredReleaseInfo_MatchAttemptID { get; set; }

    /// <summary>
    /// A comma separated list of provider IDs that were attempted to match the
    /// video.
    /// </summary>
    public string EmbeddedAttemptProviderIDs { get; set; } = string.Empty;

    /// <summary>
    /// If the attempt was successful, then this will be the ID of the release
    /// provider that matched the video to a release.
    /// </summary>
    public string? ProviderID { get; set; }

    /// <summary>
    /// Used to identify the video that was attempted to be matched, together
    /// with the <seealso cref="FileSize"/>.
    /// </summary>
    public string ED2K { get; set; } = string.Empty;

    /// <summary>
    /// The size of the video that was attempted to be matched, together with the
    /// <seealso cref="ED2K"/>.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// The time that the attempt was started.
    /// </summary>
    public DateTime AttemptStartedAt { get; set; }

    /// <summary>
    /// The time that the attempt was complete.
    /// </summary>
    public DateTime AttemptEndedAt { get; set; }

    #endregion

    /// <summary>
    /// Indicates that the attempt was successful, and a match was found.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ProviderID))]
    public bool IsSuccessful => !string.IsNullOrEmpty(ProviderID);

    /// <summary>
    /// A list of provider IDs that were attempted to match the video.
    /// </summary>
    public IReadOnlyList<string> AttemptedProviderIDs
    {
        get => EmbeddedAttemptProviderIDs.Split(',');
        set => EmbeddedAttemptProviderIDs = value.Join(',');
    }
}
