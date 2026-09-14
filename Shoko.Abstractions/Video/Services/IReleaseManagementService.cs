using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Release.Management;

namespace Shoko.Abstractions.Video.Services;

/// <summary>
///   Service for accessing the release management system — the subsystem that
///   identifies duplicate/redundant releases for a series, ranks the available
///   release candidates, and previews or queues deletion of the redundant
///   files.
/// </summary>
/// <remarks>
///   This exposes the same capabilities as the release management UI. Access
///   is not filtered by user permissions; callers interacting with user-scoped
///   data are responsible for any permission filtering required in their
///   context.
/// </remarks>
public interface IReleaseManagementService
{
    /// <summary>
    ///   Gets a paginated list of series that need review: either more than one
    ///   release candidate was identified, or the single candidate has an
    ///   episode covered by more than one file with nothing to distinguish
    ///   them. Results are sorted by series title for stable pagination.
    /// </summary>
    /// <param name="onlyFinishedSeries">
    ///   When true, only include series that have finished airing.
    /// </param>
    /// <param name="onlyWithRedundant">
    ///   When true, only include series that have at least one fully redundant
    ///   candidate.
    /// </param>
    /// <param name="includeVariations">
    ///   When true, files marked as variations participate in
    ///   grouping/redundancy like any other file.
    /// </param>
    /// <param name="search">
    ///   Filter by series title. Matched case-insensitively against all main
    ///   and official titles across all languages.
    /// </param>
    /// <param name="pageSize">
    ///   Results per page (0 = unlimited).
    /// </param>
    /// <param name="page">
    ///   Page number (1-based).
    /// </param>
    /// <returns>
    ///   The current page of series with their ranked release candidates, and
    ///   the total number of qualifying series.
    /// </returns>
    (IReadOnlyList<SeriesWithCandidates> Page, int TotalCount) GetSeriesWithCandidates(
        bool onlyFinishedSeries = false, bool onlyWithRedundant = false, bool includeVariations = false,
        string? search = null, int pageSize = 100, int page = 1);

    /// <summary>
    ///   Gets the ranked release candidates for a specific series.
    /// </summary>
    /// <param name="series">
    ///   The Shoko series to get candidates for.
    /// </param>
    /// <param name="includeVariations">
    ///   When true, files marked as variations participate in
    ///   grouping/redundancy like any other file.
    /// </param>
    /// <param name="includeOverrides">
    ///   When true, also populate the release overrides (Mix &amp; Match) data
    ///   source for the series.
    /// </param>
    /// <param name="preferredCandidateKey">
    ///   When set to one of the returned candidates' <c>Key</c>, redundancy is
    ///   recomputed treating that candidate as the kept selection instead of
    ///   the natural rank-1 candidate. Display order and rank numbers are
    ///   unaffected. Ignored if the key doesn't match any candidate.
    /// </param>
    /// <returns>
    ///   The series with its ranked release candidates, or <c>null</c> if the
    ///   series has no relevant candidates (i.e. it is unknown, has fewer than
    ///   two distinct candidates, and no ambiguous episode coverage).
    /// </returns>
    SeriesWithCandidates? GetSeriesCandidates(
        IShokoSeries series, bool includeVariations = false, bool includeOverrides = false,
        string? preferredCandidateKey = null);

    /// <summary>
    ///   Computes a preview of which files would be deleted for a single
    ///   series under the current ranking (or the ranking implied by a
    ///   preferred candidate). Does not delete anything.
    /// </summary>
    /// <param name="series">
    ///   The Shoko series to preview.
    /// </param>
    /// <param name="includeVariations">
    ///   When true, include files marked as variations in the candidate
    ///   grouping.
    /// </param>
    /// <param name="preferredCandidateKey">
    ///   When set to one of the series' candidates' key, treat that candidate
    ///   as the primary for the preview instead of the natural rank-1
    ///   candidate.
    /// </param>
    /// <returns>
    ///   The deletion preview, or <c>null</c> if nothing would be deleted.
    /// </returns>
    ReleaseDeletionPreview? GetSeriesDeletionPreview(
        IShokoSeries series, bool includeVariations = false, string? preferredCandidateKey = null);

    /// <summary>
    ///   Computes a preview of which files would be deleted when the user
    ///   manually assigns one file per episode (Mix &amp; Match release
    ///   override). The selection must cover every episode that has at least
    ///   one file; unselected files are returned as the deletion preview.
    /// </summary>
    /// <param name="series">
    ///   The Shoko series to preview.
    /// </param>
    /// <param name="selectedPlaceIDs">
    ///   The set of file location IDs to keep.
    /// </param>
    /// <param name="includeVariations">
    ///   When true, include files marked as variations.
    /// </param>
    /// <exception cref="System.ArgumentException">
    ///   One or more of <paramref name="selectedPlaceIDs"/> don't belong to
    ///   the series, or the selection doesn't cover all episodes that have
    ///   files.
    /// </exception>
    /// <returns>
    ///   The deletion preview for the manual selection.
    /// </returns>
    ReleaseDeletionPreview GetOverrideDeletionPreview(
        IShokoSeries series, IReadOnlyCollection<int> selectedPlaceIDs, bool includeVariations = false);

    /// <summary>
    ///   Queues a background job to delete the specified file locations.
    ///   Typically the place IDs come from a deletion preview, but the caller
    ///   may supply any subset.
    /// </summary>
    /// <param name="placeIDs">
    ///   The file location IDs to delete.
    /// </param>
    /// <returns>
    ///   A <see cref="Task"/> representing the asynchronous operation of
    ///   scheduling the job in the queue.
    /// </returns>
    Task QueueDeletion(IReadOnlyCollection<int> placeIDs);
}
