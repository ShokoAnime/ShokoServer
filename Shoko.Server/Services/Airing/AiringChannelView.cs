using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// A stored channel as the service hands it out.
/// </summary>
internal sealed class AiringChannelView : IAiringChannel
{
    private readonly AiringChannel _row;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringChannelView"/> class.
    /// </summary>
    /// <param name="row">The stored channel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public AiringChannelView(AiringChannel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _row = row;
    }

    /// <inheritdoc/>
    public MetadataGuid ID => ((IMetadata)_row).ID;

    /// <inheritdoc/>
    public Guid ChannelID => _row.ChannelID;

    /// <inheritdoc/>
    public string Name => _row.Name;

    /// <inheritdoc/>
    public string? CountryCode => _row.CountryCode;

    /// <inheritdoc/>
    public AiringChannelType Type => _row.Type;

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases => _row.Aliases;

    /// <inheritdoc/>
    public bool IsHidden => _row.IsHidden;

    /// <inheritdoc/>
    public DateTime CreatedAt => _row.CreatedAt;
}
