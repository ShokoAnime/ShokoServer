using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with titles.
/// </summary>
public interface IWithTitles
{
    /// <summary>
    /// The preferred or default title without the extra metadata.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// The default title as indicated by the source.
    /// </summary>
    ITitle DefaultTitle { get; }

    /// <summary>
    /// The preferred title according to the the language preference in the
    /// settings, and/or any title overrides.
    /// </summary>
    ITitle? PreferredTitle { get; }

    /// <summary>
    /// All known titles.
    /// </summary>
    IReadOnlyList<ITitle> Titles { get; }
}
