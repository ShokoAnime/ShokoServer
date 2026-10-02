using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// The links one write changed, of any source.
/// </summary>
public class MetadataLinksChangedSignalRModel
{
    /// <summary>
    /// Builds the model from the event.
    /// </summary>
    /// <param name="eventArgs">The event.</param>
    /// <param name="shokoSeriesIDs">The Shoko series of the AniDB anime the links belong to.</param>
    public MetadataLinksChangedSignalRModel(MetadataLinksChangedEventArgs eventArgs, IReadOnlyList<int> shokoSeriesIDs)
        : this(eventArgs.Reason, eventArgs.Changes, shokoSeriesIDs) { }

    /// <summary>
    /// The event narrowed to some of its changes, for a user who may not see
    /// every anime the event is about.
    /// </summary>
    /// <param name="reason">Why the links changed.</param>
    /// <param name="changes">The changes to include.</param>
    /// <param name="shokoSeriesIDs">The Shoko series of the included changes' anime.</param>
    public MetadataLinksChangedSignalRModel(MetadataLinkChangeReason reason, IEnumerable<MetadataLinkChange> changes, IReadOnlyList<int> shokoSeriesIDs)
    {
        Reason = reason;
        ShokoSeriesIDs = shokoSeriesIDs;
        Changes = [.. changes.Select(change => new LinkChange(change))];
    }

    /// <summary>
    /// Why the links were written.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public MetadataLinkChangeReason Reason { get; }

    /// <summary>
    /// The Shoko series of the AniDB anime the links belong to, those in the
    /// collection.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// The links that changed.
    /// </summary>
    public IReadOnlyList<LinkChange> Changes { get; }

    /// <summary>
    /// One link that changed.
    /// </summary>
    /// <param name="change">The change.</param>
    public class LinkChange(MetadataLinkChange change)
    {
        /// <summary>
        /// What became of the link.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public MetadataLinkChangeKind Kind { get; } = change.Kind;

        /// <summary>
        /// The source the link points at.
        /// </summary>
        public MetadataSource Source { get; } = change.Source;

        /// <summary>
        /// The level the link is made at: a series, a movie or an episode.
        /// </summary>
        public MetadataEntityType EntityType { get; } = change.EntityType;

        /// <summary>
        /// The AniDB anime the link belongs to.
        /// </summary>
        public int AnidbAnimeID { get; } = change.AnidbAnimeID;

        /// <summary>
        /// The AniDB episode of a movie or episode link.
        /// </summary>
        public int? AnidbEpisodeID { get; } = change.AnidbEpisodeID;

        /// <summary>
        /// The source's ID of the entry the link names, the new one for a
        /// replaced link, or <c>null</c> when it names none.
        /// </summary>
        public string? ID { get; } = change.ProviderID?.ID;

        /// <summary>
        /// The source's ID of the entry a replaced link named before.
        /// </summary>
        public string? PreviousID { get; } = change.PreviousProviderID?.ID;

        /// <summary>
        /// The rating after the write, or <c>null</c> when the link was
        /// removed.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public MatchRating? MatchRating { get; } = change.MatchRating;

        /// <summary>
        /// The rating before the write, or <c>null</c> when the link was
        /// added.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public MatchRating? PreviousMatchRating { get; } = change.PreviousMatchRating;
    }
}
