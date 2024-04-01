using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IWithTitles
{
    /// <summary>
    /// The default title of the show as inidicated by the source.
    /// </summary>
    string DefaultTitle { get; }

    /// <summary>
    /// The preferred title according to the user's title series preference,
    /// and/or series title overrides.
    /// </summary>
    string PreferredTitle { get; }

    /// <summary>
    /// All known titles for the show.
    /// </summary>
    IReadOnlyList<AnimeTitle> Titles { get; }
}
