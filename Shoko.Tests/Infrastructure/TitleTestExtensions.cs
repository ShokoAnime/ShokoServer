using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Reads the kinds of titles the tests check.
/// </summary>
public static class TitleTestExtensions
{
    /// <summary>
    /// An entry's synonym titles, in order.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The synonyms.</returns>
    public static IReadOnlyList<ITitle> SynonymTitles(this IWithTitles entry)
        => [.. entry.Titles.Where(title => title.Type is TitleType.Synonym)];
}
