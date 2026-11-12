using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Checks the cross-references of a release before it is saved, for the
///   sources and kinds in its <see cref="Scope"/>, and may correct them. A
///   source and kind a validator takes may be named by a cross-reference;
///   one no validator takes is refused. Only series, episodes and movies
///   may be named at all, whatever the scope.
/// </summary>
/// <remarks>
///   <para>
///     Found by the server like a release provider: implement it and nothing
///     else. Validators run in plugin load order, the core's first. Several
///     may take the same source and kind; all of them must find a
///     cross-reference valid, and each sees it as the ones before corrected
///     it.
///   </para>
///   <para>
///     Validators are owned by the plugins using the links, not by the
///     metadata providers of their sources. They run when a release is saved,
///     and again on a stored release to heal it, such as filling in the series
///     of an episode (<see cref="ReleaseCrossReferenceValidationContext.IsHealing"/>);
///     a stored release is never refused after the fact, so the stored releases
///     of a source whose plugin is gone still load.
///   </para>
/// </remarks>
public interface IReleaseCrossReferenceValidator
{
    /// <summary>
    ///   The name of the validator, typically matching the plugin name. Used
    ///   in the logs and in the reason a release is refused.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The sources and kinds the validator checks. Read once, at
    ///   registration, so return a constant.
    /// </summary>
    MetadataEntityScope Scope { get; }

    /// <summary>
    ///   Checks one cross-reference naming at least one entry of the
    ///   validator's <see cref="Scope"/>.
    /// </summary>
    /// <remarks>
    ///   A thrown exception refuses the release and fails the save. When the
    ///   service needed to check or correct the cross-reference cannot be
    ///   reached, find it valid as it is rather than invalid.
    /// </remarks>
    /// <param name="context">
    ///   The cross-reference, the release it belongs to and the video.
    /// </param>
    /// <param name="cancellationToken">
    ///   Cancels the check.
    /// </param>
    /// <returns>
    ///   <c>null</c> when the cross-reference is not the validator's concern,
    ///   or the result.
    /// </returns>
    Task<ReleaseCrossReferenceValidationResult?> Validate(ReleaseCrossReferenceValidationContext context, CancellationToken cancellationToken);
}
