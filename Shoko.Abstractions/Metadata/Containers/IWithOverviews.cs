using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with overviews.
/// </summary>
public interface IWithOverviews
{
    /// <summary>
    /// The default overview as indicated by the source.
    /// </summary>
    IText? DefaultOverview { get; }

    /// <summary>
    /// The preferred overview according to the language preference in
    /// the settings, and/or any overview overrides.
    /// </summary>
    IText? PreferredOverview { get; }

    /// <summary>
    /// All known overviews.
    /// </summary>
    IReadOnlyList<IText> Overviews { get; }
}
