using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.TMDB;

/// <summary>
///   A TMDB genre or keyword, read as a tag. TMDB's tables keep only their
///   names, so the name is the identity: <c>tmdb://tag/genre/&lt;name&gt;</c>
///   or <c>tmdb://tag/keyword/&lt;name&gt;</c>.
/// </summary>
/// <param name="name">The genre's or keyword's name.</param>
/// <param name="kind">Whether it is a genre or a keyword.</param>
public sealed class TMDB_Tag(string name, TagKind kind) : ITag
{
    #region Helpers

    /// <summary>
    ///   The tags of an entry with TMDB genres and keywords: the genres
    ///   first, then the keywords, each in TMDB's order.
    /// </summary>
    /// <param name="genres">The genres' names.</param>
    /// <param name="keywords">The keywords' names.</param>
    /// <returns>The tags, blank names left out.</returns>
    internal static IReadOnlyList<ITag> For(IEnumerable<string> genres, IEnumerable<string> keywords)
        => [
            .. genres.Where(genre => !string.IsNullOrWhiteSpace(genre)).Select(genre => new TMDB_Tag(genre.Trim(), TagKind.Genre)),
            .. keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => new TMDB_Tag(keyword.Trim(), TagKind.Keyword)),
        ];

    /// <summary>
    ///   The source's own ID for a genre or keyword: its kind and its name,
    ///   or its kind and a hash of its name when the name is too long.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="kind">Whether it is a genre or a keyword.</param>
    /// <returns>The ID.</returns>
    internal static string IDFor(string name, TagKind kind)
    {
        var prefix = kind is TagKind.Genre ? GenrePrefix : KeywordPrefix;
        var id = prefix + name;
        return id.Length <= MetadataGuid.MaxIDLength
            ? id
            : prefix + "#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
    }

    /// <summary>
    ///   The genre or keyword an ID names, when one of the given names is it.
    ///   A name is only hashed when the ID is a hashed one and the name is
    ///   too long to be named as it is.
    /// </summary>
    /// <param name="id">The source's own ID, e.g. <c>genre/Drama</c>.</param>
    /// <param name="names">The genres' or keywords' names in use, for the kind asked.</param>
    /// <returns>The tag, or <see langword="null"/> when the ID is not one or no name is it.</returns>
    internal static TMDB_Tag? Find(string id, Func<TagKind, IEnumerable<string>> names)
    {
        var kind = id.StartsWith(GenrePrefix, StringComparison.Ordinal) ? TagKind.Genre
            : id.StartsWith(KeywordPrefix, StringComparison.Ordinal) ? TagKind.Keyword
            : (TagKind?)null;
        if (kind is not { } tagKind)
            return null;

        var prefixLength = tagKind is TagKind.Genre ? GenrePrefix.Length : KeywordPrefix.Length;
        var wanted = id[prefixLength..];
        if (wanted.Length == 0)
            return null;

        var hashed = wanted[0] == '#' && wanted.Length == 65;
        foreach (var raw in names(tagKind))
        {
            var name = raw.Trim();
            if (name.Length == 0)
                continue;
            if (prefixLength + name.Length <= MetadataGuid.MaxIDLength)
            {
                if (string.Equals(name, wanted, StringComparison.Ordinal))
                    return new(name, tagKind);
            }
            else if (hashed && string.Equals(IDFor(name, tagKind), id, StringComparison.Ordinal))
            {
                return new(name, tagKind);
            }
        }

        return null;
    }

    private const string GenrePrefix = "genre/";

    private const string KeywordPrefix = "keyword/";

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Tag, IDFor(name, kind));

    #endregion

    #region ITag Implementation

    string ITag.Name => name;

    string ITag.Overview => string.Empty;

    TagKind ITag.Kind => kind;

    #endregion
}
