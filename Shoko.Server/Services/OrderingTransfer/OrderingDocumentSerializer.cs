using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Services.OrderingTransfer;

/// <summary>
///   Writes an ordering document as plain JSON or as a zip archive with its
///   image files, and reads either back.
/// </summary>
internal static partial class OrderingDocumentSerializer
{
    #region Constants

    /// <summary>
    ///   The document's name in a zip archive.
    /// </summary>
    public const string ManifestName = "manifest.json";

    /// <summary>
    ///   The folder of the image files in a zip archive.
    /// </summary>
    public const string ImageFolder = "images/";

    /// <summary>
    ///   The largest document read, uncompressed.
    /// </summary>
    internal const long MaxManifestBytes = 64L * 1024 * 1024;

    /// <summary>
    ///   The largest image file read from an archive, uncompressed.
    /// </summary>
    internal const long MaxImageBytes = 64L * 1024 * 1024;

    /// <summary>
    ///   How the document is written and read: camel case, enums as text,
    ///   nulls left out.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [GeneratedRegex(@"^images/[0-9a-f]{64}\.[a-z0-9]{2,5}$")]
    private static partial Regex ImagePathRegex();

    #endregion

    #region Writing

    /// <summary>
    ///   The path of an image file in a zip archive.
    /// </summary>
    /// <param name="sha256">The file's SHA-256, in lowercase hex.</param>
    /// <param name="contentType">The file's media type.</param>
    /// <returns>The path, <c>images/&lt;sha256&gt;.&lt;ext&gt;</c>.</returns>
    public static string ImagePath(string sha256, string? contentType)
        => $"{ImageFolder}{sha256}.{Extension(contentType)}";

    /// <summary>
    ///   The file extension for an image's media type.
    /// </summary>
    /// <param name="contentType">The media type.</param>
    /// <returns>The extension, without its dot; <c>bin</c> when not known.</returns>
    public static string Extension(string? contentType)
        => contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            "image/tiff" => "tiff",
            "image/svg+xml" => "svg",
            _ => "bin",
        };

    /// <summary>
    ///   Writes a document as plain JSON.
    /// </summary>
    /// <param name="destination">Where to write it; left open.</param>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the document is written.</returns>
    public static Task WriteJson(Stream destination, OrderingDocument document, CancellationToken cancellationToken)
        => JsonSerializer.SerializeAsync(destination, document, JsonOptions, cancellationToken);

    /// <summary>
    ///   Starts a zip archive, which may be written to a stream that cannot
    ///   seek, such as a response body.
    /// </summary>
    /// <param name="destination">Where to write it; left open.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The archive, to dispose once written.</returns>
    public static Task<ZipArchive> CreateArchive(Stream destination, CancellationToken cancellationToken)
        => ZipArchive.CreateAsync(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken);

    /// <summary>
    ///   Copies an image file into a zip archive, uncompressed, as image
    ///   files are compressed already.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="path">The file's path, from <see cref="ImagePath"/>.</param>
    /// <param name="file">The file, read to its end.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the file is written.</returns>
    public static async Task WriteImage(ZipArchive archive, string path, Stream file, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        var stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
            await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///   Writes a document into a zip archive as its manifest, after the image
    ///   files it names.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the manifest is written.</returns>
    public static async Task WriteManifest(ZipArchive archive, OrderingDocument document, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
        var stream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Reading

    /// <summary>
    ///   Reads a document, telling a zip archive from plain JSON by its first
    ///   bytes, and checks its format and version.
    /// </summary>
    /// <param name="source">The file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The document, with the archive it came in if any.</returns>
    /// <exception cref="OrderingDocumentException">The file is not an ordering export this server can read.</exception>
    public static async Task<OrderingPayload> Read(Stream source, CancellationToken cancellationToken)
    {
        var stream = source;
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            stream = buffer;
        }

        var start = stream.Position;
        var magic = new byte[4];
        var read = await stream.ReadAtLeastAsync(magic, magic.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        stream.Position = start;
        if (read is 0)
            throw new OrderingDocumentException("The payload is empty.");

        if (read is 4 && magic[0] is (byte)'P' && magic[1] is (byte)'K' && magic[2] is 3 && magic[3] is 4)
            return await ReadArchive(stream, cancellationToken).ConfigureAwait(false);

        var document = await Deserialize(stream, cancellationToken).ConfigureAwait(false);
        return new(document, null);
    }

    /// <summary>
    ///   Reads a zip archive's manifest, keeping the archive for the files.
    /// </summary>
    /// <param name="stream">The archive.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The document, with the archive.</returns>
    /// <exception cref="OrderingDocumentException">The archive cannot be read or has no manifest.</exception>
    private static async Task<OrderingPayload> ReadArchive(Stream stream, CancellationToken cancellationToken)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new OrderingDocumentException($"The zip archive cannot be read: {ex.Message}");
        }

        try
        {
            if (archive.GetEntry(ManifestName) is not { } manifest)
                throw new OrderingDocumentException($"The zip archive has no {ManifestName}.");
            if (manifest.Length > MaxManifestBytes)
                throw new OrderingDocumentException($"The {ManifestName} is larger than {MaxManifestBytes / 1024 / 1024} MiB.");

            await using var manifestStream = manifest.Open();
            var document = await Deserialize(manifestStream, cancellationToken).ConfigureAwait(false);
            return new(document, archive);
        }
        catch (InvalidDataException ex)
        {
            archive.Dispose();
            throw new OrderingDocumentException($"The {ManifestName} cannot be read: {ex.Message}");
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    /// <summary>
    ///   Reads the JSON document and checks its format and version.
    /// </summary>
    /// <param name="stream">The JSON.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The document.</returns>
    /// <exception cref="OrderingDocumentException">The JSON cannot be read or is not an ordering export this server can read.</exception>
    internal static async Task<OrderingDocument> Deserialize(Stream stream, CancellationToken cancellationToken)
    {
        OrderingDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<OrderingDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new OrderingDocumentException($"The payload is not a valid ordering export: {ex.Message}");
        }

        if (document is null)
            throw new OrderingDocumentException("The payload is empty.");
        if (string.IsNullOrEmpty(document.Format))
            throw new OrderingDocumentException($"The payload names no format, so it is not a \"{OrderingDocument.FormatName}\" export.");
        if (document.Version is null)
            throw new OrderingDocumentException("The payload names no version of the format.");
        if (!string.Equals(document.Format, OrderingDocument.FormatName, StringComparison.Ordinal))
            throw new OrderingDocumentException($"The payload's format is \"{document.Format}\", not \"{OrderingDocument.FormatName}\".");
        if (document.Version is < 1)
            throw new OrderingDocumentException($"The payload's version {document.Version} is not valid.");
        if (document.Version > OrderingDocument.CurrentVersion)
            throw new OrderingDocumentException(
                $"The payload is version {document.Version} of the format, and this server reads up to version {OrderingDocument.CurrentVersion}."
            );

        document.Orderings ??= [];
        foreach (var ordering in document.Orderings)
        {
            if (ordering is null)
                throw new OrderingDocumentException("The payload holds an empty ordering.");

            ordering.Images ??= [];
            ordering.Groups ??= [];
            foreach (var group in ordering.Groups)
            {
                if (group is null)
                    throw new OrderingDocumentException("The payload holds an empty group.");

                group.Episodes ??= [];
                group.Images ??= [];
                group.Episodes.RemoveAll(episode => episode is null);
                group.Images.RemoveAll(image => image is null);
            }

            ordering.Images.RemoveAll(image => image is null);
        }

        return document;
    }

    /// <summary>
    ///   Whether a path names an image file of an archive.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> if it does.</returns>
    internal static bool IsImagePath(string path)
        => ImagePathRegex().IsMatch(path);

    #endregion
}

/// <summary>
///   A read ordering export, with the zip archive it came in, if any, for the
///   image files.
/// </summary>
/// <param name="document">The document.</param>
/// <param name="archive">The archive, which the payload disposes.</param>
internal sealed class OrderingPayload(OrderingDocument document, ZipArchive? archive) : IDisposable
{
    /// <summary>
    ///   The document.
    /// </summary>
    public OrderingDocument Document => document;

    /// <summary>
    ///   Reads the file of an image from the payload.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="problem">Why there is no file, when there is none.</param>
    /// <returns>The file, or <c>null</c>.</returns>
    public byte[]? GetFile(OrderingDocumentImage image, out string? problem)
    {
        problem = null;
        if (!string.IsNullOrEmpty(image.File))
        {
            if (archive is null)
            {
                problem = $"It names the file \"{image.File}\", but the payload is not a zip archive.";
                return null;
            }

            if (!OrderingDocumentSerializer.IsImagePath(image.File) || archive.GetEntry(image.File) is not { } entry)
            {
                problem = $"The payload has no file \"{image.File}\".";
                return null;
            }

            if (entry.Length > OrderingDocumentSerializer.MaxImageBytes)
            {
                problem = $"The file \"{image.File}\" is larger than {OrderingDocumentSerializer.MaxImageBytes / 1024 / 1024} MiB.";
                return null;
            }

            try
            {
                using var stream = entry.Open();
                using var buffer = new MemoryStream((int)entry.Length);
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            catch (InvalidDataException ex)
            {
                problem = $"The file \"{image.File}\" cannot be read: {ex.Message}";
                return null;
            }
        }

        if (!string.IsNullOrEmpty(image.Data))
        {
            try
            {
                return Convert.FromBase64String(image.Data);
            }
            catch (FormatException)
            {
                problem = "Its inline file is not valid base64.";
                return null;
            }
        }

        problem = "The payload has no file for it.";
        return null;
    }

    /// <inheritdoc />
    public void Dispose()
        => archive?.Dispose();
}

/// <summary>
///   Thrown when a payload is not an ordering export this server can read.
/// </summary>
/// <param name="message">What is wrong with it.</param>
internal sealed class OrderingDocumentException(string message) : Exception(message);
