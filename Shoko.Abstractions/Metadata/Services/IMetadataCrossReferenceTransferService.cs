using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Writes a source's links to AniDB into a CSV file and reads them back,
///   so they can be moved between servers or corrected by hand.
/// </summary>
/// <remarks>
///   A file has up to three sections, each opened by a header naming its
///   columns after the source, e.g. for <c>tmdb</c>:
///   <c>AnidbAnimeId,AnidbEpisodeId,TmdbMovieId,Rating</c>,
///   <c>AnidbAnimeId,TmdbShowId,Rating</c> and
///   <c>AnidbAnimeId,AnidbEpisodeId,TmdbShowId,TmdbEpisodeId,Rating</c>. Other
///   sources use their pascal-cased name. Lines starting with <c>#</c> are comments.
/// </remarks>
public interface IMetadataCrossReferenceTransferService
{
    /// <summary>
    ///   Write a source's links into a cross-reference file.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="options">Which links to write and how, or <c>null</c> for every link without comments.</param>
    /// <returns>
    ///   The file's text, empty when nothing matched. A provider ID is the
    ///   source's own ID, quoted when it holds a comma or a quote; a link to
    ///   nothing is an empty ID, and a <c>0</c> is read as one too.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    string Export(MetadataSource source, MetadataCrossReferenceExportOptions? options = null);

    /// <summary>
    ///   Read a cross-reference file into a source's links.
    /// </summary>
    /// <remarks>
    ///   Every line is read before anything is written, so a file with an
    ///   unreadable line changes nothing. A link already there keeps its place
    ///   and, unless a person verified it, takes the file's rating. A series
    ///   link the file adds, directly or through an episode link, counts as
    ///   verified by a person.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="reader">
    ///   The file's text. Only an episode can be linked to nothing: a series
    ///   line naming no series is skipped (older files hold them), and a film
    ///   line naming no film cannot be read.
    /// </param>
    /// <param name="options">How to read it, or <c>null</c> for the defaults.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was written, or the lines that could not be read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="reader"/> is <c>null</c>.</exception>
    Task<MetadataCrossReferenceImportResult> Import(
        MetadataSource source,
        TextReader reader,
        MetadataCrossReferenceImportOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
