using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.User;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Services;

/// <summary>
///   Gathers what the writes to the link store changed, and hands it on as
///   one event per operation.
/// </summary>
/// <remarks>
///   Every store write in an operation's flow joins it; a nested one joins the
///   outer one unless it gives a reason the outer one lacks (as an API call's).
///   A write outside any operation is reported alone, for its flow's reason or
///   <see cref="MetadataLinkChangeReason.Other"/>. Ending one ends it for every
///   copy of the flow, so a task begun meanwhile reports its later writes apart.
/// </remarks>
/// <param name="logger">Optional. Where a handler's failure is logged.</param>
public class MetadataLinkChangeTracker(ILogger<MetadataLinkChangeTracker>? logger = null)
{
    #region Fields

    /// <summary>
    ///   The operation of the current flow, if one was begun.
    /// </summary>
    private readonly AsyncLocal<Operation?> _operation = new();

    /// <summary>
    ///   The latest reason set for the current flow, if one was.
    /// </summary>
    private readonly AsyncLocal<ReasonScope?> _defaultReason = new();

    /// <summary>
    ///   Where a handler's failure is logged.
    /// </summary>
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    #endregion

    #region Events

    /// <summary>
    ///   Raised once per operation that changed links, and once per store
    ///   write made outside one.
    /// </summary>
    public event EventHandler<MetadataLinksChangedEventArgs>? Changed;

    #endregion

    #region Scopes

    /// <summary>
    ///   Begins an operation, whose writes are reported together once it is
    ///   disposed.
    /// </summary>
    /// <param name="reason">
    ///   Why the links are written, or <c>null</c> for the reason
    ///   set for the flow.
    /// </param>
    /// <returns>
    ///   The operation, or a scope that does nothing when the flow is in one
    ///   already that it joins.
    /// </returns>
    public IDisposable Begin(MetadataLinkChangeReason? reason = null)
    {
        var open = _operation.Value is { IsOpen: true } current ? current : null;
        if (open is not null && (reason is null || open.ReasonGiven))
            return Scope.None;

        var operation = new Operation(this, reason ?? DefaultReason ?? MetadataLinkChangeReason.Other, reason is not null, open, ActorContext.CurrentActor);
        _operation.Value = operation;
        return operation;
    }

    /// <summary>
    ///   Sets the reason for the links written in the current flow that no
    ///   operation gives one for.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>A scope putting back the reason there was before.</returns>
    public IDisposable UseDefaultReason(MetadataLinkChangeReason reason)
    {
        var scope = new ReasonScope(this, reason, _defaultReason.Value);
        _defaultReason.Value = scope;
        return scope;
    }

    /// <summary>
    ///   The reason set for the current flow that has not ended, if any.
    /// </summary>
    private MetadataLinkChangeReason? DefaultReason
    {
        get
        {
            for (var scope = _defaultReason.Value; scope is not null; scope = scope.Previous)
            {
                if (scope.IsActive)
                    return scope.Reason;
            }

            return null;
        }
    }

    #endregion

    #region Recording

    /// <summary>
    ///   Records what one store write changed, reporting it at once when no
    ///   operation is open.
    /// </summary>
    /// <param name="changes">The rows the write changed.</param>
    internal void Record(IReadOnlyCollection<LinkRowChange> changes)
    {
        if (changes.Count is 0)
            return;

        if (_operation.Value is { } operation && operation.TryAdd(changes))
            return;

        Raise(DefaultReason ?? MetadataLinkChangeReason.Other, changes, ActorContext.CurrentActor);
    }

    /// <summary>
    ///   Raises the event for what an operation changed, if anything did.
    /// </summary>
    /// <param name="reason">Why the links were written.</param>
    /// <param name="changes">The rows changed, in the order they were written.</param>
    /// <param name="actor">Who made the changes, or <c>null</c> for the system.</param>
    private void Raise(MetadataLinkChangeReason reason, IReadOnlyCollection<LinkRowChange> changes, ApiToken? actor)
    {
        var handlers = Changed;
        if (handlers is null)
            return;

        var consolidated = Consolidate(changes);
        if (consolidated.Count is 0)
            return;

        var eventArgs = new MetadataLinksChangedEventArgs { Reason = reason, Changes = consolidated, Actor = actor };
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<MetadataLinksChangedEventArgs>>())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A handler of the link change event failed.");
            }
        }
    }

    /// <summary>
    ///   Turns the rows an operation wrote into the links it changed: each
    ///   link from how it was before the first write to how it was after the
    ///   last, dropping the ones that ended as they began, and a link removed
    ///   and one added at the same place paired as a replacement, told where
    ///   the first of the two was written.
    /// </summary>
    /// <param name="changes">The rows changed, in the order they were written.</param>
    /// <returns>The links changed, in the order they were first written.</returns>
    internal static IReadOnlyList<MetadataLinkChange> Consolidate(IEnumerable<LinkRowChange> changes)
    {
        var links = new List<LinkRowChange>();
        var indexes = new Dictionary<(MetadataEntityType, MetadataSource, int, int?, MetadataGuid?), int>();
        foreach (var change in changes)
        {
            if (indexes.TryGetValue(change.Key, out var index))
                links[index] = links[index] with { After = change.After };
            else
            {
                indexes[change.Key] = links.Count;
                links.Add(change);
            }
        }

        var result = links
            .Where(link => link.Before != link.After)
            .Select(link => (MetadataLinkChange?)link.ToChange(link switch
            {
                { Before: null } => MetadataLinkChangeKind.Added,
                { After: null } => MetadataLinkChangeKind.Removed,
                _ => MetadataLinkChangeKind.RatingChanged,
            }))
            .ToList();

        // At each place, removed and added links pair up in write order, each
        // pair placed where the first of the two was.
        var places = result
            .Select((change, index) => (Change: change!, Index: index))
            .Where(pair => pair.Change.Kind is not MetadataLinkChangeKind.RatingChanged)
            .GroupBy(pair => (pair.Change.EntityType, pair.Change.Source, pair.Change.AnidbAnimeID, pair.Change.AnidbEpisodeID))
            .ToList();
        foreach (var place in places)
        {
            var removed = place.Where(pair => pair.Change.Kind is MetadataLinkChangeKind.Removed).ToList();
            var added = place.Where(pair => pair.Change.Kind is MetadataLinkChangeKind.Added).ToList();
            foreach (var (gone, come) in removed.Zip(added))
            {
                result[Math.Min(gone.Index, come.Index)] = come.Change with
                {
                    Kind = MetadataLinkChangeKind.Replaced,
                    PreviousProviderID = gone.Change.ProviderID,
                    PreviousMatchRating = gone.Change.PreviousMatchRating,
                };
                result[Math.Max(gone.Index, come.Index)] = null;
            }
        }

        return [.. result.OfType<MetadataLinkChange>()];
    }

    #endregion

    #region Nested types

    /// <summary>
    ///   One row a store write changed.
    /// </summary>
    /// <param name="EntityType">The level of the link.</param>
    /// <param name="Source">The source the link points at.</param>
    /// <param name="AnidbAnimeID">The AniDB anime.</param>
    /// <param name="AnidbEpisodeID">The AniDB episode, or <c>null</c> at the series level.</param>
    /// <param name="ProviderID">The entry the link names, or <c>null</c> for none.</param>
    /// <param name="Before">The rating before the write, or <c>null</c> when the row is new.</param>
    /// <param name="After">The rating after the write, or <c>null</c> when the row is gone.</param>
    internal sealed record LinkRowChange(
        MetadataEntityType EntityType,
        MetadataSource Source,
        int AnidbAnimeID,
        int? AnidbEpisodeID,
        MetadataGuid? ProviderID,
        MatchRating? Before,
        MatchRating? After
    )
    {
        /// <summary>
        ///   The link the row is.
        /// </summary>
        public (MetadataEntityType, MetadataSource, int, int?, MetadataGuid?) Key => (EntityType, Source, AnidbAnimeID, AnidbEpisodeID, ProviderID);

        /// <summary>
        ///   A row as a store write left it.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="before">Its rating before the write, or <c>null</c> when it is new.</param>
        /// <param name="after">Its rating after the write, or <c>null</c> when it is gone.</param>
        /// <returns>The change.</returns>
        public static LinkRowChange Of(CrossRef_AniDB_Metadata row, MatchRating? before, MatchRating? after)
            => new(
                row.EntityType,
                row.Source,
                row.AnidbAnimeID,
                row switch
                {
                    CrossRef_AniDB_Metadata_Movie movie => movie.AnidbEpisodeID,
                    CrossRef_AniDB_Metadata_Episode episode => episode.AnidbEpisodeID,
                    _ => null,
                },
                ((IMetadataCrossReference)row).ProviderID,
                before,
                after
            );

        /// <summary>
        ///   The change as reported.
        /// </summary>
        /// <param name="kind">What became of the link.</param>
        /// <returns>The change.</returns>
        public MetadataLinkChange ToChange(MetadataLinkChangeKind kind)
            => new()
            {
                Kind = kind,
                Source = Source,
                EntityType = EntityType,
                AnidbAnimeID = AnidbAnimeID,
                AnidbEpisodeID = AnidbEpisodeID,
                ProviderID = ProviderID,
                MatchRating = After,
                PreviousMatchRating = Before,
            };
    }

    /// <summary>
    ///   The writes of one operation, reported once it ends.
    /// </summary>
    /// <param name="tracker">The tracker it reports to.</param>
    /// <param name="reason">Why the links are written.</param>
    /// <param name="reasonGiven">
    ///   Whether <paramref name="reason"/> was given by whoever began it,
    ///   rather than taken from the flow.
    /// </param>
    /// <param name="parent">The operation it was begun in, which the flow returns to once it ends.</param>
    /// <param name="actor">
    ///   Who the operation runs for, taken when it is begun, so it is reported
    ///   for them even if it ends after their request did.
    /// </param>
    private sealed class Operation(MetadataLinkChangeTracker tracker, MetadataLinkChangeReason reason, bool reasonGiven, Operation? parent, ApiToken? actor) : IDisposable
    {
        private readonly List<LinkRowChange> _changes = [];

        /// <summary>
        ///   Why the links are written.
        /// </summary>
        public MetadataLinkChangeReason Reason => reason;

        /// <summary>
        ///   Whether the reason was given rather than taken from the flow.
        /// </summary>
        public bool ReasonGiven => reasonGiven;

        /// <summary>
        ///   Whether writes still join it.
        /// </summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>
        ///   Adds what a write changed, unless the operation has ended.
        /// </summary>
        /// <param name="changes">The rows changed.</param>
        /// <returns><c>true</c> when they were added.</returns>
        public bool TryAdd(IReadOnlyCollection<LinkRowChange> changes)
        {
            lock (_changes)
            {
                if (!IsOpen)
                    return false;

                _changes.AddRange(changes);
                return true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            List<LinkRowChange> changes;
            lock (_changes)
            {
                if (!IsOpen)
                    return;

                IsOpen = false;
                changes = [.. _changes];
            }

            if (tracker._operation.Value == this)
                tracker._operation.Value = parent is { IsOpen: true } ? parent : null;
            tracker.Raise(reason, changes, actor);
        }
    }

    /// <summary>
    ///   A reason set for a flow, until it is disposed.
    /// </summary>
    /// <param name="tracker">The tracker it is set on.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="previous">The reason set before it, which the flow returns to.</param>
    private sealed class ReasonScope(MetadataLinkChangeTracker tracker, MetadataLinkChangeReason reason, ReasonScope? previous) : IDisposable
    {
        /// <summary>
        ///   The reason.
        /// </summary>
        public MetadataLinkChangeReason Reason => reason;

        /// <summary>
        ///   The reason set before it.
        /// </summary>
        public ReasonScope? Previous => previous;

        /// <summary>
        ///   Whether it still applies, in every copy of the flow.
        /// </summary>
        public bool IsActive { get; private set; } = true;

        /// <inheritdoc />
        public void Dispose()
        {
            IsActive = false;
            if (tracker._defaultReason.Value == this)
                tracker._defaultReason.Value = previous;
        }
    }

    /// <summary>
    ///   A scope that does nothing, for an operation joining the open one.
    /// </summary>
    private sealed class Scope : IDisposable
    {
        /// <summary>
        ///   The scope.
        /// </summary>
        public static readonly Scope None = new();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    #endregion
}
