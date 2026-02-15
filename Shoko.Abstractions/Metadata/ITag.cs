
namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Basic tag metadata.
/// </summary>
public interface ITag : IMetadata<int>
{

    /// <summary>
    /// The tag name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What does the tag mean/what's it for.
    /// </summary>
    string Description { get; }
}
