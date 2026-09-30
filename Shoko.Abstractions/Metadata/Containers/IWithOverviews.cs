using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with overviews.
/// </summary>
#pragma warning disable CS0618 // Forwards the members of the interface it replaces.
public interface IWithOverviews : IWithDescriptions
#pragma warning restore CS0618
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

#pragma warning disable CS0618 // Forwards the members of the interface it replaces.
    IText? IWithDescriptions.DefaultDescription { get => DefaultOverview; }

    IText? IWithDescriptions.PreferredDescription { get => PreferredOverview; }

    IReadOnlyList<IText> IWithDescriptions.Descriptions { get => Overviews; }
#pragma warning restore CS0618
}
