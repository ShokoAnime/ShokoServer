using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Orderings;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Writes orderings, with their names, overviews and images, into a file
///   another server can read back as local orderings.
/// </summary>
/// <remarks>
///   A JSON document (<c>{ "format": "shoko-orderings", "version": 1,
///   "exportedAt", "server", "orderings": [...] }</c>), alone or as
///   <c>manifest.json</c> in a zip beside its embedded images
///   (<c>images/&lt;sha256&gt;.&lt;ext&gt;</c>). Series and episodes are named
///   by AniDB IDs, episodes also by type and number, so no local IDs leak.
/// </remarks>
public interface IMetadataOrderingTransferService
{
    /// <summary>
    ///   Write orderings into an export file.
    /// </summary>
    /// <param name="destination">Where the file is written.</param>
    /// <param name="options">Which orderings to write and how, or <see langword="null"/> for every local ordering with its images by URL.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> cannot be written.</exception>
    Task<MetadataOrderingExportResult> Export(
        Stream destination,
        MetadataOrderingExportOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Write orderings into an export file held in memory, embedded images
    ///   included; write a large one to a stream with <see cref="Export"/>.
    /// </summary>
    /// <param name="options">Which orderings to write and how, or <see langword="null"/> for every local ordering with its images by URL.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The file and what was written.</returns>
    async Task<MetadataOrderingExportFile> ExportToBytes(
        MetadataOrderingExportOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = new MemoryStream();
        var result = await Export(stream, options, cancellationToken).ConfigureAwait(false);
        return new(stream.ToArray(), result);
    }

    /// <summary>
    ///   Read an export file, JSON or zip, into local orderings of the
    ///   matching Shoko series.
    /// </summary>
    /// <remarks>
    ///   The whole file is read before anything is written, so one that cannot
    ///   be read changes nothing. An ordering whose anime is not in the
    ///   collection is skipped, and an episode that cannot be found is dropped
    ///   from its group; the result reports both.
    /// </remarks>
    /// <param name="source">
    ///   The file. Its images go through the image manager: one of a remote
    ///   source this server can fetch from is found or added under that
    ///   source, keeping its URL, and takes the embedded file as its copy or
    ///   has its download queued. Any other embedded file is uploaded as a
    ///   user's image.
    /// </param>
    /// <param name="options">How to read it, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What was done with each ordering, or why the file could not be read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<MetadataOrderingImportResult> Import(
        Stream source,
        MetadataOrderingImportOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
