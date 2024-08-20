using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Container object with descriptions.
/// </summary>
public interface IWithDescriptions
{
    /// <summary>
    /// The default description as indicated by the source.
    /// </summary>
    string DefaultDescription { get; }

    /// <summary>
    /// The preferred description according to the the language preference in
    /// the settings, and/or any description overrides.
    /// </summary>
    string PreferredDescription { get; }

    /// <summary>
    /// All known descriptions.
    /// </summary>
    IReadOnlyList<TextDescription> Descriptions { get; }
}
