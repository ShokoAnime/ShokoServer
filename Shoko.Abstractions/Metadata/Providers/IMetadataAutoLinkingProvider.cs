using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Works out what an anime is in a source: as many series and films as it
///   takes. You score the candidates and the core links the ones you take,
///   so you need not write or store any link yourself.
/// </summary>
public interface IMetadataAutoLinkingProvider : IMetadataProvider
{
    /// <summary>
    ///   Run your own matching for an anime and hand back every candidate you
    ///   scored, taken or not.
    /// </summary>
    /// <remarks>
    ///   Called from your search job for a new anime, for a search a person
    ///   asks for, and for a preview. Work inline and write nothing: the core
    ///   links each candidate without a <see cref="MetadataAutoLinkCandidate.Rejection"/>
    ///   (a film to the AniDB episode it names, or the whole anime), matches a
    ///   linked series' episodes and refreshes the links. A person's search of
    ///   one anime replaces its links on your source once something is written.
    /// </remarks>
    /// <param name="anidbAnimeID">
    ///   The AniDB anime to work out. Its current links are not yours to
    ///   weigh: answer as if it had none.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The candidates you scored (not every raw search hit), best first, each
    ///   one you turned down with its reason; an empty list when nothing was
    ///   found or the anime is not stored. The core also turns down kinds the
    ///   admin turned off, invalid entries and ones nothing on your source can
    ///   write. Only <see cref="MetadataAutoLinkOrigin.Search"/> candidates are
    ///   taken on their own. Another anime's links listed for context (such as
    ///   a prequel's, as <see cref="MetadataAutoLinkOrigin.PrequelLink"/>) are
    ///   rated <see cref="MatchRating.None"/>, carry the link's rating in
    ///   <see cref="MetadataAutoLinkCandidate.LinkMatchRating"/> and are turned
    ///   down as <see cref="MatchRejectionReason.ExistingLink"/>. Entries the
    ///   anime's AniDB resources name go in as
    ///   <see cref="MetadataAutoLinkOrigin.AnidbResource"/>, and those its links
    ///   on other sources name as <see cref="MetadataAutoLinkOrigin.CrossSourceLink"/>
    ///   (from <see cref="Services.IMetadataLinkingService.GetCrossSourceHints"/>),
    ///   with your own rating, the one to take first listed first. The core
    ///   takes one such hint at most, never as <see cref="MatchRating.UserVerified"/>,
    ///   and only while the anime has no link on your source (or the search
    ///   replaces them) and the search took nothing or only competing picks
    ///   rated below it (those become <see cref="MatchRejectionReason.Outranked"/>).
    ///   The other hints become <see cref="MatchRejectionReason.HintNotNeeded"/>.
    /// </returns>
    /// <exception cref="MetadataProviderUnavailableException">Your source cannot be reached for now.</exception>
    Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default);
}
