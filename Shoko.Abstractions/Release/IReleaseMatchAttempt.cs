using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Release;

/// <summary>
/// Information about an attempt to match a video to a release.
/// </summary>
public interface IReleaseMatchAttempt
{
    /// <summary>
    /// If the attempt was successful and ran through the auto-matching, then
    /// this will be the ID of the release provider that matched the video to a
    /// release. It may be  <c>null</c> and still successfully matched if it
    /// was matched by other means.
    /// </summary>
    public Guid? ProviderID { get; }

    /// <summary>
    /// If the attempt was successful, then this will be the name of the release
    /// provider that matched the video to a release.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// A list of provider names that were attempted to match the video.
    /// </summary>
    public IReadOnlyList<string> AttemptedProviderNames { get; }

    /// <summary>
    /// Indicates that the attempt was successful, and a match was found.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ProviderName))]
    bool IsSuccessful { get; }

    /// <summary>
    /// The time that the attempt was started.
    /// </summary>
    public DateTime AttemptStartedAt { get; set; }

    /// <summary>
    /// The time that the attempt was complete.
    /// </summary>
    public DateTime AttemptEndedAt { get; set; }
}
