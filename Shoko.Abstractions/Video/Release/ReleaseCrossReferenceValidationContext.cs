using System.Collections.Generic;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   What a <see cref="IReleaseCrossReferenceValidator"/> checks: one
///   cross-reference of a release about to be saved, with the whole release
///   in view.
/// </summary>
public sealed class ReleaseCrossReferenceValidationContext
{
    /// <summary>
    ///   The video the release is being saved for.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    ///   The release being saved. Its own cross-references are as they were
    ///   given; read <see cref="CrossReferences"/> for them as the validators
    ///   before this one corrected them.
    /// </summary>
    public required IReleaseInfo Release { get; init; }

    /// <summary>
    ///   Every cross-reference of the release, as the validators before this
    ///   one corrected them.
    /// </summary>
    public required IReadOnlyList<IReleaseVideoCrossReference> CrossReferences { get; init; }

    /// <summary>
    ///   The position of <see cref="CrossReference"/> in
    ///   <see cref="CrossReferences"/>.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    ///   The cross-reference to check.
    /// </summary>
    public IReleaseVideoCrossReference CrossReference => CrossReferences[Index];

    /// <summary>
    ///   Whether the release is a stored one asked about again, to fill in
    ///   what its cross-references lack, such as the series of an episode,
    ///   rather than one about to be saved. Only a correction counts then: an
    ///   invalid result or a throw leaves the cross-reference as it is, so a
    ///   validator should fill in what it can and otherwise find it valid as
    ///   it is, and may skip slow checks.
    /// </summary>
    public bool IsHealing { get; init; }
}
