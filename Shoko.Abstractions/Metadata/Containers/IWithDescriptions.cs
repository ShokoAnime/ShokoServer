using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with descriptions. Replaced by <see cref="IWithOverviews"/>,
/// which forwards these members.
/// </summary>
[Obsolete("Use IWithOverviews instead.")]
public interface IWithDescriptions
{
    /// <summary>
    /// The default description as indicated by the source.
    /// </summary>
    [Obsolete("Use IWithOverviews.DefaultOverview instead.")]
    IText? DefaultDescription { get; }

    /// <summary>
    /// The preferred description according to the language preference in
    /// the settings, and/or any description overrides.
    /// </summary>
    [Obsolete("Use IWithOverviews.PreferredOverview instead.")]
    IText? PreferredDescription { get; }

    /// <summary>
    /// All known descriptions.
    /// </summary>
    [Obsolete("Use IWithOverviews.Overviews instead.")]
    IReadOnlyList<IText> Descriptions { get; }
}
