using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Server.Services;

/// <summary>
///   Holds the release cross-reference validators of every plugin, in plugin
///   load order, and runs them over a release about to be saved.
/// </summary>
/// <param name="logger">The logger.</param>
public class ReleaseCrossReferenceValidators(ILogger<ReleaseCrossReferenceValidators> logger)
{
    #region Fields

    private IReadOnlyList<(IReleaseCrossReferenceValidator Validator, MetadataEntityScope Scope)> _validators = [];

    private bool _loaded;

    #endregion

    #region Properties

    /// <summary>
    ///   The validators taken, in the order they run.
    /// </summary>
    public IReadOnlyList<IReleaseCrossReferenceValidator> Validators => [.. _validators.Select(entry => entry.Validator)];

    #endregion

    #region Add Parts

    /// <summary>
    ///   Takes the validators, in plugin load order. One whose scope cannot be
    ///   read or is empty is refused and logged.
    /// </summary>
    /// <param name="validators">The validators.</param>
    public void AddParts(IEnumerable<IReleaseCrossReferenceValidator> validators)
    {
        if (_loaded)
            return;

        _loaded = true;
        var taken = new List<(IReleaseCrossReferenceValidator, MetadataEntityScope)>();
        foreach (var validator in validators)
        {
            MetadataEntityScope scope;
            try
            {
                scope = validator.Scope;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refusing release cross-reference validator {Validator}: reading its scope failed.", validator.Name);
                continue;
            }

            if (scope is not { IsEmpty: false })
            {
                logger.LogError("Refusing release cross-reference validator {Validator}: it names no source or kind.", validator.Name);
                continue;
            }

            taken.Add((validator, scope));
        }

        _validators = taken;
        logger.LogInformation("Loaded {Count} release cross-reference validators.", taken.Count);
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Runs every validator over the cross-references of a release, in
    ///   order, each over the cross-references naming an entry of its scope.
    ///   When a validator changes the range of a cross-reference, the ones
    ///   that already found it valid are asked again.
    /// </summary>
    /// <param name="video">The video the release is saved for.</param>
    /// <param name="release">The release.</param>
    /// <param name="crossReferences">The cross-references to check, already tidied by the core.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <param name="heal">
    ///   Whether the release is a stored one asked about again: only the
    ///   corrections count, nothing refuses it, and a step that would refuse
    ///   it leaves its cross-reference as it was and is logged.
    /// </param>
    /// <returns>The cross-references, as the validators corrected them.</returns>
    /// <exception cref="ReleaseValidationException">
    ///   A cross-reference names no entry, an entry of a kind a release may not
    ///   link to (anything but a series, an episode or a movie), or an entry
    ///   no validator takes, or a
    ///   validator found one invalid, corrected one outside its scope, changed
    ///   a range another validator then changed again, or threw.
    /// </exception>
    public async Task<IReadOnlyList<IReleaseVideoCrossReference>> Validate(
        IVideo video,
        IReleaseInfo release,
        IReadOnlyList<IReleaseVideoCrossReference> crossReferences,
        CancellationToken cancellationToken = default,
        bool heal = false
    )
    {
        var validators = _validators;
        for (var index = 0; !heal && index < crossReferences.Count; index++)
        {
            var ids = crossReferences[index].IDs;
            if (ids.Count is 0)
                throw new ReleaseValidationException(null, $"Cross-reference {index + 1} names no entry.");

            foreach (var id in ids)
            {
                if (!IsLinkableKind(id.EntityType))
                    throw new ReleaseValidationException(null, $"Cross-reference {index + 1} names {id}, but a release may only link to series, episodes and movies.");
                if (!validators.Any(entry => entry.Scope.Contains(id)))
                    throw new ReleaseValidationException(null, $"No release cross-reference validator takes {id.Source.Value}://{id.EntityType.Value}, named by cross-reference {index + 1} ({id}).");
            }
        }

        var current = crossReferences.ToArray();

        // The validators that found each cross-reference valid so far, asked again when its range changes.
        var approvals = current.Select(_ => new List<(IReleaseCrossReferenceValidator Validator, MetadataEntityScope Scope)>()).ToArray();
        foreach (var (validator, scope) in validators)
        {
            for (var index = 0; index < current.Length; index++)
            {
                if (!current[index].IDs.Any(scope.Contains))
                    continue;

                var before = current[index];
                try
                {
                    if (await Step(validator, scope, index).ConfigureAwait(false))
                        approvals[index].Add((validator, scope));
                }
                catch (ReleaseValidationException ex) when (heal)
                {
                    // A stored release is never refused after the fact: the step is undone.
                    logger.LogWarning("Release cross-reference validator {Validator} could not heal cross-reference {Index} of the release of video {Video}: {Reason}", ex.ValidatorName, index + 1, video.ID, ex.Reason);
                    current[index] = before;
                }
            }
        }

        return current;

        // Asks one validator about one cross-reference, applies its correction, and asks the ones that
        // passed it before again when its range changed. False when the cross-reference is not its concern.
        async Task<bool> Step(IReleaseCrossReferenceValidator validator, MetadataEntityScope scope, int index)
        {
            var before = current[index];
            if (await Check(validator, scope, index, null).ConfigureAwait(false) is false)
                return false;

            if (!HasOtherRange(before, current[index]))
                return true;

            var range = current[index];
            foreach (var (other, otherScope) in approvals[index])
            {
                if (!current[index].IDs.Any(otherScope.Contains))
                    continue;

                var prior = current[index];
                await Check(other, otherScope, index, validator.Name).ConfigureAwait(false);
                if (HasOtherRange(prior, current[index]))
                    throw new ReleaseValidationException(
                        other.Name,
                        $"It changed the range of cross-reference {index + 1} that {validator.Name} set ({Range(range)}) to {Range(current[index])}. The two disagree."
                    );
            }

            return true;
        }

        // Asks one validator about one cross-reference and applies its correction.
        // False when the cross-reference is not its concern.
        async Task<bool> Check(IReleaseCrossReferenceValidator validator, MetadataEntityScope scope, int index, string? rangeChangedBy)
        {
            var again = rangeChangedBy is null ? string.Empty : $", asked again after {rangeChangedBy} changed its range";
            ReleaseCrossReferenceValidationResult? result;
            try
            {
                result = await validator.Validate(new() { Video = video, Release = release, CrossReferences = current, Index = index, IsHealing = heal }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Release cross-reference validator {Validator} threw while checking cross-reference {Index} of a release for video {Video}.", validator.Name, index + 1, video.ID);
                throw new ReleaseValidationException(validator.Name, $"It threw while checking cross-reference {index + 1}{again}: {ex.GetType().Name}.", ex);
            }

            if (result is null)
                return false;

            if (!result.IsValid)
                throw new ReleaseValidationException(validator.Name, $"Cross-reference {index + 1} is invalid{again}: {result.Reason}");

            if (!result.IsCorrected)
                return true;

            var crossReference = current[index];
            var ids = result.CorrectedIDs;
            var outside = crossReference.IDs.Except(ids)
                .Concat(ids.Except(crossReference.IDs))
                .FirstOrDefault(id => !scope.Contains(id));
            if (outside is not null)
                throw new ReleaseValidationException(validator.Name, $"Its correction of cross-reference {index + 1} changed {outside}, outside its scope.");
            if (ids.Count is 0)
                throw new ReleaseValidationException(validator.Name, $"Its correction of cross-reference {index + 1} names no entry.");

            current[index] = new ReleaseVideoCrossReference
            {
                IDs = [.. ids],
                PercentageStart = result.CorrectedPercentageStart ?? crossReference.PercentageStart,
                PercentageEnd = result.CorrectedPercentageEnd ?? crossReference.PercentageEnd,
            };
            return true;
        }

        static bool HasOtherRange(IReleaseVideoCrossReference a, IReleaseVideoCrossReference b)
            => a.PercentageStart != b.PercentageStart || a.PercentageEnd != b.PercentageEnd;

        static string Range(IReleaseVideoCrossReference crossReference)
            => $"{crossReference.PercentageStart}-{crossReference.PercentageEnd}%";
    }

    #endregion

    /// <summary>
    ///   Whether a release may link to an entry of a kind: a series, an
    ///   episode or a movie, of any source.
    /// </summary>
    /// <param name="type">The kind of entry.</param>
    /// <returns><c>true</c> when it may.</returns>
    internal static bool IsLinkableKind(MetadataEntityType type)
        => type == MetadataEntityType.Series || type == MetadataEntityType.Episode || type == MetadataEntityType.Movie;
}
