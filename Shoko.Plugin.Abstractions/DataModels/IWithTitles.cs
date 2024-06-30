using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IWithTitles
{
    /// <summary>
    /// The default title as indicated by the source.
    /// </summary>
    string DefaultTitle { get; }

    /// <summary>
    /// The preferred title according to the the language preference in the
    /// settings, and/or any title overrides.
    /// </summary>
    string PreferredTitle { get; }

    /// <summary>
    /// All known titles.
    /// </summary>
    IReadOnlyList<AnimeTitle> Titles { get; }
}
