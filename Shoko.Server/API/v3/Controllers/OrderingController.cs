using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Orderings;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Moves orderings, with their names, descriptions and images, between
/// servers. Only an admin may export or import.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize("admin")]
public class OrderingController(
    ISettingsProvider settingsProvider,
    AnimeSeriesRepository seriesRepository,
    IMetadataOrderingTransferService transferService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// The largest file an import takes.
    /// </summary>
    internal const long MaxImportBytes = 512L * 1024 * 1024;

    /// <summary>
    /// How much of a raw import body is held in memory before the rest is
    /// buffered to a temporary file.
    /// </summary>
    private const int RawBufferThreshold = 1024 * 1024;

    /// <summary>
    /// Export orderings, of one series or several, to a file another server
    /// can import. With no orderings or series named, every local ordering is
    /// exported.
    /// </summary>
    /// <param name="orderingIDs">Orderings to export, of any kind: full IDs, or a user's ordering's local ID.</param>
    /// <param name="seriesIDs">Shoko series whose local orderings to export.</param>
    /// <param name="includeGlobal">Also export the stored global orderings of <paramref name="seriesIDs"/>.</param>
    /// <param name="images">How to carry the images: <c>None</c>, <c>UrlOnly</c>, <c>EmbedMissingRemote</c> or <c>EmbedAll</c>.</param>
    /// <param name="container">The file: <c>Auto</c> (zip when embedding, else JSON), <c>Json</c> or <c>Zip</c>.</param>
    /// <param name="includePreferred">Record which ordering each series uses.</param>
    /// <returns>The file, <c>application/zip</c> or <c>application/json</c>.</returns>
    [HttpGet("Export")]
    [Produces("application/json", "application/zip")]
    public ActionResult Export(
        [FromQuery] List<string>? orderingIDs = null,
        [FromQuery] List<int>? seriesIDs = null,
        [FromQuery] bool includeGlobal = false,
        [FromQuery] MetadataOrderingImageExportMode images = MetadataOrderingImageExportMode.UrlOnly,
        [FromQuery] MetadataOrderingContainer container = MetadataOrderingContainer.Auto,
        [FromQuery] bool includePreferred = true
    )
    {
        var orderings = new List<MetadataGuid>();
        foreach (var text in orderingIDs ?? [])
        {
            if (SeriesOrderingController.ParseOrderingID(text) is not { } id)
                return ValidationProblem($"\"{text}\" is not a valid ordering ID.", nameof(orderingIDs));
            orderings.Add(id);
        }

        var series = new List<MetadataGuid>();
        foreach (var seriesID in seriesIDs ?? [])
        {
            if (seriesRepository.GetByID(seriesID) is not ISeries found)
                return ValidationProblem($"No series has the ID {seriesID}.", nameof(seriesIDs));
            series.Add(found.ID);
        }

        return Export(
            transferService,
            new()
            {
                OrderingIDs = orderings,
                SeriesIDs = series,
                IncludeGlobalOrderings = includeGlobal,
                ImageMode = images,
                Container = container,
                IncludePreferred = includePreferred,
            }
        );
    }

    /// <summary>
    /// Import orderings from a file an export wrote, JSON or zip, sent as a
    /// multipart form. Each is made a local ordering of the matching Shoko
    /// series.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="conflict">For a local ordering of the series with the same name: <c>Skip</c>, <c>Replace</c> or <c>KeepBoth</c>.</param>
    /// <param name="images">Where to restore images from: <c>PayloadFirst</c>, <c>UrlFirst</c>, <c>PayloadOnly</c>, <c>UrlOnly</c> or <c>None</c>.</param>
    /// <param name="verifyHashes">Refuse an image file whose SHA-256 is not the one given.</param>
    /// <param name="applyPreferred">Choose each ordering that was its series' chosen one.</param>
    /// <param name="dryRun">Only report what would be done.</param>
    /// <returns>What was done with each ordering.</returns>
    [HttpPost("Import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public async Task<ActionResult<OrderingImportResult>> Import(
        IFormFile file,
        [FromQuery] MetadataOrderingConflictMode conflict = MetadataOrderingConflictMode.Skip,
        [FromQuery] MetadataOrderingImageImportMode images = MetadataOrderingImageImportMode.PayloadFirst,
        [FromQuery] bool verifyHashes = true,
        [FromQuery] bool applyPreferred = false,
        [FromQuery] bool dryRun = false
    )
    {
        if (file is null || file.Length == 0)
            return ValidationProblem("The file cannot be empty.", nameof(file));

        await using var stream = file.OpenReadStream();
        return await Import(stream, conflict, images, verifyHashes, applyPreferred, dryRun);
    }

    /// <summary>
    /// Import orderings from a file an export wrote, JSON or zip, sent as the
    /// raw request body. Each is made a local ordering of the matching Shoko
    /// series.
    /// </summary>
    /// <param name="conflict">For a local ordering of the series with the same name: <c>Skip</c>, <c>Replace</c> or <c>KeepBoth</c>.</param>
    /// <param name="images">Where to restore images from: <c>PayloadFirst</c>, <c>UrlFirst</c>, <c>PayloadOnly</c>, <c>UrlOnly</c> or <c>None</c>.</param>
    /// <param name="verifyHashes">Refuse an image file whose SHA-256 is not the one given.</param>
    /// <param name="applyPreferred">Choose each ordering that was its series' chosen one.</param>
    /// <param name="dryRun">Only report what would be done.</param>
    /// <returns>What was done with each ordering.</returns>
    [HttpPost("Import/Raw")]
    [Consumes("application/json", "application/zip", "application/octet-stream")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<OrderingImportResult>> ImportRaw(
        [FromQuery] MetadataOrderingConflictMode conflict = MetadataOrderingConflictMode.Skip,
        [FromQuery] MetadataOrderingImageImportMode images = MetadataOrderingImageImportMode.PayloadFirst,
        [FromQuery] bool verifyHashes = true,
        [FromQuery] bool applyPreferred = false,
        [FromQuery] bool dryRun = false
    )
    {
        var request = HttpContext.Request;
        if (request.ContentLength is 0)
            return ValidationProblem("The request body cannot be empty.");

        // Buffer the body, past a small threshold on disk, so the import gets
        // a seekable stream and does not copy the whole file into memory.
        request.EnableBuffering(RawBufferThreshold, MaxImportBytes);
        await request.Body.DrainAsync(HttpContext.RequestAborted);
        request.Body.Position = 0;
        return await Import(request.Body, conflict, images, verifyHashes, applyPreferred, dryRun);
    }

    #region Helpers

    /// <summary>
    /// Hands out an export as a file, written straight into the response.
    /// </summary>
    /// <param name="service">The transfer service.</param>
    /// <param name="options">What to export.</param>
    /// <returns>The file.</returns>
    internal static ActionResult Export(IMetadataOrderingTransferService service, MetadataOrderingExportOptions options)
    {
        // The file's type goes out before its content, so settle it first.
        var file = new MetadataOrderingExportResult { Container = MetadataOrderingTransferService.ContainerFor(options) };
        var name = $"shoko-orderings-{DateTime.UtcNow:yyyyMMdd-HHmmss}{file.FileExtension}";
        return new ExportFileResult(service, options with { Container = file.Container }, file.ContentType) { FileDownloadName = name };
    }

    /// <summary>
    /// An export written into the response body as it is made, so the file is
    /// never held whole in memory.
    /// </summary>
    /// <param name="service">The transfer service.</param>
    /// <param name="options">What to export, with its container settled.</param>
    /// <param name="contentType">The file's media type.</param>
    private sealed class ExportFileResult(IMetadataOrderingTransferService service, MetadataOrderingExportOptions options, string contentType)
        : FileResult(contentType)
    {
        /// <inheritdoc />
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = ContentType;
            var disposition = new ContentDispositionHeaderValue("attachment");
            disposition.SetHttpFileName(FileDownloadName);
            response.Headers.ContentDisposition = disposition.ToString();
            await service.Export(response.Body, options, context.HttpContext.RequestAborted);
        }
    }

    /// <summary>
    /// Reads an import and reports what was done.
    /// </summary>
    /// <param name="stream">The file.</param>
    /// <param name="conflict">What to do with a local ordering of the same name.</param>
    /// <param name="images">Where to restore images from.</param>
    /// <param name="verifyHashes">Whether to check the image files' hashes.</param>
    /// <param name="applyPreferred">Whether to choose the orderings that were chosen.</param>
    /// <param name="dryRun">Whether to only report.</param>
    /// <returns>What was done, or why the file could not be read.</returns>
    private async Task<ActionResult<OrderingImportResult>> Import(
        Stream stream,
        MetadataOrderingConflictMode conflict,
        MetadataOrderingImageImportMode images,
        bool verifyHashes,
        bool applyPreferred,
        bool dryRun
    )
    {
        var result = await transferService.Import(
            stream,
            new()
            {
                ConflictMode = conflict,
                ImageMode = images,
                VerifyHashes = verifyHashes,
                ApplyPreferred = applyPreferred,
                DryRun = dryRun,
            },
            HttpContext.RequestAborted
        );
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("Body", error);
            return ValidationProblem(ModelState);
        }

        return new OrderingImportResult(result);
    }

    #endregion
}
