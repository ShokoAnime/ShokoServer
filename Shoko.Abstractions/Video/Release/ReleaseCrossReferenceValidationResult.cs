using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   What a <see cref="IReleaseCrossReferenceValidator"/> found a
///   cross-reference to be. <see cref="IsValid"/> decides which of the other
///   properties are read: the corrected IDs and range for a valid one, and
///   <see cref="Reason"/> for an invalid one.
/// </summary>
public sealed class ReleaseCrossReferenceValidationResult
{
    /// <summary>
    ///   Whether the cross-reference is valid. An invalid one refuses the
    ///   whole release.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Reason))]
    public bool IsValid { get; private init; }

    /// <summary>
    ///   Whether a valid cross-reference is saved corrected, with
    ///   <see cref="CorrectedIDs"/> and, when set, the corrected range.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CorrectedIDs))]
    public bool IsCorrected => CorrectedIDs is not null;

    /// <summary>
    ///   For a corrected cross-reference, the IDs to save in its place. Only
    ///   those of the validator's own scope may differ.
    /// </summary>
    public IReadOnlyList<MetadataGuid>? CorrectedIDs { get; private init; }

    /// <summary>
    ///   For a corrected cross-reference, where the video starts covering the
    ///   content, or <c>null</c> to keep its range.
    /// </summary>
    public int? CorrectedPercentageStart { get; private init; }

    /// <summary>
    ///   For a corrected cross-reference, where the video stops covering the
    ///   content, or <c>null</c> to keep its range.
    /// </summary>
    public int? CorrectedPercentageEnd { get; private init; }

    /// <summary>
    ///   For an invalid cross-reference, why, for a person to read.
    /// </summary>
    public string? Reason { get; private init; }

    private ReleaseCrossReferenceValidationResult() { }

    /// <summary>
    ///   The cross-reference is valid as it is.
    /// </summary>
    /// <returns>The result.</returns>
    public static ReleaseCrossReferenceValidationResult Valid()
        => new() { IsValid = true };

    /// <summary>
    ///   The cross-reference is valid with these IDs, keeping its range.
    /// </summary>
    /// <param name="ids">
    ///   The IDs to save in its place. Only those of the validator's own scope
    ///   may differ.
    /// </param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ids"/> is <c>null</c>.</exception>
    public static ReleaseCrossReferenceValidationResult Corrected(IEnumerable<MetadataGuid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return new() { IsValid = true, CorrectedIDs = [.. ids] };
    }

    /// <summary>
    ///   The cross-reference is valid with these IDs and this range. The range
    ///   belongs to the whole cross-reference, so when it changes, the
    ///   validators that already found the cross-reference valid are asked
    ///   again, and one changing the range again refuses the release.
    /// </summary>
    /// <param name="ids">
    ///   The IDs to save in its place. Only those of the validator's own scope
    ///   may differ.
    /// </param>
    /// <param name="percentageStart">Where the video starts covering the content, in [0, 99].</param>
    /// <param name="percentageEnd">Where the video stops covering the content, in [1, 100], after the start.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ids"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is out of bounds or empty.</exception>
    public static ReleaseCrossReferenceValidationResult Corrected(IEnumerable<MetadataGuid> ids, int percentageStart, int percentageEnd)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentOutOfRangeException.ThrowIfNegative(percentageStart);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentageEnd, 100);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(percentageEnd, percentageStart);
        return new()
        {
            IsValid = true,
            CorrectedIDs = [.. ids],
            CorrectedPercentageStart = percentageStart,
            CorrectedPercentageEnd = percentageEnd,
        };
    }

    /// <summary>
    ///   The cross-reference is invalid, which refuses the release.
    /// </summary>
    /// <param name="reason">Why, for a person to read.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    public static ReleaseCrossReferenceValidationResult Invalid(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new() { IsValid = false, Reason = reason };
    }
}
