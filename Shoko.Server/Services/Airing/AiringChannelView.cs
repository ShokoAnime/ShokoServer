using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// A stored channel as the service hands it out, with whether the server
/// hides it as the setting stood when it was read.
/// </summary>
internal sealed class AiringChannelView : IAiringChannel
{
    private readonly AiringChannel _row;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringChannelView"/> class.
    /// </summary>
    /// <param name="row">The stored channel.</param>
    /// <param name="isHidden">Whether the server hides the channel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public AiringChannelView(AiringChannel row, bool isHidden)
    {
        ArgumentNullException.ThrowIfNull(row);

        _row = row;
        IsHidden = isHidden;
    }

    /// <inheritdoc/>
    public MetadataGuid ID => ((IMetadata)_row).ID;

    /// <inheritdoc/>
    public Guid ChannelID => _row.ChannelID;

    /// <inheritdoc/>
    public string Name => _row.Name;

    /// <inheritdoc/>
    public AiringChannelType Type => _row.Type;

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases => _row.Aliases;

    /// <inheritdoc/>
    public bool IsHidden { get; }

    /// <inheritdoc/>
    public DateTime CreatedAt => _row.CreatedAt;
}
