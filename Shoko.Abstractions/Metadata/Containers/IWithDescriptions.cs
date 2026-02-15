using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with descriptions.
/// </summary>
public interface IWithDescriptions
{
    /// <summary>
    /// The default description as indicated by the source.
    /// </summary>
    IText? DefaultDescription { get; }

    /// <summary>
    /// The preferred description according to the the language preference in
    /// the settings, and/or any description overrides.
    /// </summary>
    IText? PreferredDescription { get; }

    /// <summary>
    /// All known descriptions.
    /// </summary>
    IReadOnlyList<IText> Descriptions { get; }
}
