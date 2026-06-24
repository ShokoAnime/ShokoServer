
using System.Collections.Generic;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
/// Video cross-reference included in an <see cref="IReleaseInfo"/>.
/// </summary>
public interface IReleaseVideoCrossReference
{
    /// <summary>
    /// Provider-scoped IDs that identify what content this file segment maps to.
    /// Well-known keys are defined in <see cref="CrossReferenceIDs"/>.
    /// Custom providers may add their own keys; consumers ignore keys they
    /// do not recognise.
    /// </summary>
    IReadOnlyDictionary<string, string> ProviderIDs { get; }

    /// <summary>
    /// Where in the mapped content the video starts covering in
    /// the range [0, 99], but must be less than <see cref="PercentageEnd"/>.
    /// </summary>
    int PercentageStart { get; }

    /// <summary>
    /// Where in the mapped content the video stops covering in
    /// the range [1, 100], but must be greater than <see cref="PercentageStart"/>.
    /// </summary>
    int PercentageEnd { get; }
}
