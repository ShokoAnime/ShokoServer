using System;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Thrown when a release is refused on save because of its
///   cross-references: one names no entry, names a source and kind no
///   validator takes, or a validator found it invalid or threw.
/// </summary>
/// <param name="validatorName">The validator that refused the release, if any.</param>
/// <param name="reason">Why.</param>
/// <param name="innerException">What the validator threw, if it threw.</param>
public class ReleaseValidationException(string? validatorName, string reason, Exception? innerException = null)
    : InvalidOperationException(
        validatorName is null ? $"The release was refused: {reason}" : $"The release was refused by validator \"{validatorName}\": {reason}",
        innerException
    )
{
    /// <summary>
    ///   The name of the validator that refused the release, or <c>null</c>
    ///   when the core refused it before any validator ran.
    /// </summary>
    public string? ValidatorName { get; } = validatorName;

    /// <summary>
    ///   Why the release was refused.
    /// </summary>
    public string Reason { get; } = reason;
}
