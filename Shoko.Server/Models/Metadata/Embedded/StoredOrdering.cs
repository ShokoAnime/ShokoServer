using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Services;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A stored ordering, read back with its groups.
/// </summary>
/// <param name="row">The ordering's row.</param>
/// <param name="service">The ordering service, which reads the groups and knows the choice and the hidden episodes.</param>
public sealed class StoredOrdering(Metadata_Ordering row, MetadataOrderingService service) : IOrdering, IInlineTextSource
{
    private IReadOnlyList<StoredOrderingGroup>? _groups;

    private IReadOnlyList<IEpisode>? _episodes;

    /// <summary>
    ///   The ordering's row.
    /// </summary>
    internal Metadata_Ordering Row => row;

    /// <summary>
    ///   The ordering's groups, in viewing order.
    /// </summary>
    public IReadOnlyList<StoredOrderingGroup> Groups => _groups ??= service.ReadGroups(this);

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID => row.ID;

    #endregion

    #region IOrdering Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => row.SeriesGuid;

    /// <inheritdoc />
    public string Name => row.Name;

    /// <inheritdoc />
    public string Overview => row.Description ?? string.Empty;

    /// <inheritdoc />
    public OrderingType Type => row.Type;

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <inheritdoc />
    public bool IsPreferred => service.IsChosen(SeriesID, ID);

    /// <inheritdoc />
    public int EpisodeCount => Episodes.Count;

    /// <inheritdoc />
    public int HiddenEpisodeCount => service.CountHidden(Episodes);

    /// <inheritdoc />
    public int SeasonCount => Groups.Count;

    /// <inheritdoc />
    public ISeries? Series => service.GetSeries(SeriesID);

    /// <inheritdoc />
    public IReadOnlyList<ISeason> Seasons => Groups;

    /// <inheritdoc />
    public IReadOnlyList<IEpisode> Episodes => _episodes ??= [.. Groups.SelectMany(group => group.Episodes).DistinctBy(episode => episode.ID)];

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(row.Source, row.Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(row.Source, row.Description, TitleLanguage.Unknown, "unk");

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => row.CreatedAt.ToUniversalTime();

    /// <inheritdoc />
    public DateTime LastUpdatedAt => row.LastUpdatedAt.ToUniversalTime();

    #endregion
}
