using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class CollectionInfoUpdatedEventSignalRModel(CollectionInfoUpdatedEventArgs eventArgs)
{
    /// <summary>
    /// The provider metadata source.
    /// </summary>
    public MetadataSource Source { get; } = eventArgs.CollectionInfo.Source;

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; } = eventArgs.Reason;

    /// <summary>
    /// The provided metadata collection id.
    /// </summary>
    public string CollectionID { get; } = eventArgs.CollectionInfo.ID.ID;
}
