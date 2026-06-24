using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
/// Video cross-reference included in an <see cref="IReleaseInfo"/>.
/// </summary>
public class ReleaseVideoCrossReference : IReleaseVideoCrossReference
{
    /// <inheritdoc />
    public Dictionary<string, string> ProviderIDs { get; set; } = new();

    /// <summary>
    /// Where in the mapped content the video starts covering.
    /// If null, the video starts at the beginning of the content.
    /// Can be in the range [0, 99], but must be less than the
    /// <see cref="PercentageEnd"/> if both are set.
    /// </summary>
    [Range(0, 99)]
    public int? PercentageStart { get; set; }

    /// <summary>
    /// Where in the mapped content the video stops covering.
    /// If null, the video stops at the end of the content.
    /// Can be in the range [1, 100], but must be greater than the
    /// <see cref="PercentageStart"/> if both are set.
    /// </summary>
    [Range(1, 100)]
    public int? PercentageEnd { get; set; }

    /// <inheritdoc />
    public ReleaseVideoCrossReference() { }

    /// <inheritdoc />
    public ReleaseVideoCrossReference(IReleaseVideoCrossReference reference)
    {
        ProviderIDs = new Dictionary<string, string>(reference.ProviderIDs);
        if (reference is ReleaseVideoCrossReference vRef)
        {
            PercentageStart = vRef.PercentageStart;
            PercentageEnd = vRef.PercentageEnd;
        }
        else
        {
            PercentageStart = reference.PercentageStart;
            PercentageEnd = reference.PercentageEnd;
        }
    }

    #region IReleaseVideoCrossReference Implementation

    IReadOnlyDictionary<string, string> IReleaseVideoCrossReference.ProviderIDs => ProviderIDs;

    int IReleaseVideoCrossReference.PercentageStart => PercentageStart ?? 0;

    int IReleaseVideoCrossReference.PercentageEnd => PercentageEnd ?? 100;

    #endregion
}
