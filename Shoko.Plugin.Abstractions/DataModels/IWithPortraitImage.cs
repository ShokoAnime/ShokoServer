
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents an entity with a portrait image.
/// </summary>
public interface IWithPortraitImage
{
    /// <summary>
    /// Portrait image of the casted role, if
    /// available from the provider.
    /// </summary>
    IImageMetadata? PortraitImage { get; }
}
