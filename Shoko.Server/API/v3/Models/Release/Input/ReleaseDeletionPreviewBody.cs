using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release.Input;

/// <summary>
/// Request body for computing a release deletion preview.
/// </summary>
public class ReleaseDeletionPreviewBody
{
    /// <summary>
    /// Series IDs to exclude from the preview. These series will not appear
    /// in the result even if they have redundant candidates.
    /// </summary>
    public List<int>? ExcludedSeriesIDs { get; set; }

    /// <summary>
    /// Per-series overrides that select a specific candidate as the primary
    /// (to keep). When provided for a series, the specified candidate is
    /// treated as rank 1 regardless of the auto-ranking order.
    /// </summary>
    public List<SeriesCandidateOverride>? Overrides { get; set; }

    /// <summary>
    /// Per-series override specifying which candidate to treat as the primary.
    /// </summary>
    public class SeriesCandidateOverride
    {
        /// <summary>Shoko series ID.</summary>
        [Required]
        public required int SeriesID { get; set; }

        /// <summary>
        /// The <see cref="ReleaseCandidateDTO.Key"/> of the candidate to treat as
        /// the primary (best) release. All other candidates will be evaluated as
        /// secondary against this one.
        /// </summary>
        [Required]
        public required string PreferredCandidateKey { get; set; }
    }
}
