using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Plugin;
using Shoko.Plugin.Abstractions.Relocation;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Relocation;
using Shoko.Server.API.v3.Models.Relocation.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;

using ApiRelocationPipe = Shoko.Server.API.v3.Models.Relocation.RelocationPipe;
using ApiRelocationResult = Shoko.Server.API.v3.Models.Relocation.RelocationResult;
using RelocationPipe = Shoko.Plugin.Abstractions.Relocation.RelocationPipe;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
///   Controller responsible for handling file relocation. Interacts with the <see cref="IRelocationService"/>.
/// </summary>
/// <param name="settingsProvider">
///   Settings provider.
/// </param>
/// <param name="pluginManager">
///   Plugin manager.
/// </param>
/// <param name="configurationService">
///   Configuration Service.
/// </param>
/// <param name="videoService">
///   Video service.
/// </param>
/// <param name="relocationService">
///   Relocation service.
/// </param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RelocationController(ISettingsProvider settingsProvider, IPluginManager pluginManager, IConfigurationService configurationService, IVideoService videoService, IRelocationService relocationService) : BaseController(settingsProvider)
{
    #region Settings

    /// <summary>
    ///   Gets a summary of the relocation service's properties.
    /// </summary>
    /// <returns>
    ///   A <see cref="RelocationSummary"/> containing the current settings.
    /// </returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Summary")]
    public ActionResult<RelocationSummary> GetReleaseInfoSummary()
        => new RelocationSummary
        {
            RenameOnImport = relocationService.RenameOnImport,
            MoveOnImport = relocationService.MoveOnImport,
            AllowRelocationInsideDestinationOnImport = relocationService.AllowRelocationInsideDestinationOnImport,
            ProviderCount = relocationService.GetAvailableProviders().Count(),
        };

    /// <summary>
    ///   Updates the relocation settings, such as the rename and move options.
    /// </summary>
    /// <param name="body">
    ///   The settings to update.
    /// </param>
    /// <returns>
    ///   An empty <see cref="ActionResult"/>.
    /// </returns>
    [Authorize(Roles = "admin,init")]
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpPost("Settings")]
    public ActionResult UpdateReleaseInfoSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateRelocationSettingsBody body)
    {
        if (body.RenameOnImport.HasValue)
            relocationService.RenameOnImport = body.RenameOnImport.Value;
        if (body.MoveOnImport.HasValue)
            relocationService.MoveOnImport = body.MoveOnImport.Value;
        if (body.AllowRelocationInsideDestinationOnImport.HasValue)
            relocationService.AllowRelocationInsideDestinationOnImport = body.AllowRelocationInsideDestinationOnImport.Value;

        return Ok();
    }

    #endregion

    #region Providers

    /// <summary>
    ///   Gets all relocation providers available.
    /// </summary>
    /// <returns>
    ///   A list of all available <see cref="RelocationProvider"/>s.
    /// </returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider")]
    public ActionResult<List<RelocationProvider>> GetAvailableReleaseProviders([FromQuery] Guid? pluginID = null)
        => pluginID.HasValue
            ? pluginManager.GetPluginInfo(pluginID.Value) is { IsActive: true } pluginInfo
                ? relocationService.GetProviderInfo(pluginInfo.Plugin)
                    .Select(providerInfo => new RelocationProvider(providerInfo))
                    .ToList()
                : []
            : relocationService.GetAvailableProviders()
                .Select(providerInfo => new RelocationProvider(providerInfo))
                .ToList();

    /// <summary>
    ///   Gets a specific relocation provider by ID.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the relocation provider to get.
    /// </param>
    /// <returns>
    ///   A <see cref="RelocationProvider"/>.
    /// </returns>
    [DatabaseBlockedExempt]
    [InitFriendly]
    [HttpGet("Provider/{providerID}")]
    public ActionResult<RelocationProvider> GetRenamer([FromRoute] Guid providerID)
    {
        if (relocationService.GetProviderInfo(providerID) is not { } value)
            return NotFound("Renamer not found");

        return new RelocationProvider(value);
    }

    #endregion

    #region Preview

    /// <summary>
    ///   Preview running a relocation provider on a batch of files, using the
    ///   default pipe or the provided provider identified by ID and provided
    ///   configuration — if the provider identified by ID necessitates it.
    /// </summary>
    /// <param name="body">
    ///   The body, with the file IDs and optionally the provider ID and
    ///   configuration to use.
    /// </param>
    /// <param name="move">
    ///   Whether or not to get the destination of the files. If <c>null</c>, to
    ///   <see cref="RelocationSummary.MoveOnImport"/>.
    /// </param>
    /// <param name="rename">
    ///   Whether or not to get the new name of the files. If <c>null</c>,
    ///   defaults to <see cref="RelocationSummary.RenameOnImport"/>.
    /// </param>
    /// <param name="allowRelocationInsideDestination">
    ///   Whether or not to allow relocation of files inside the destination. If
    ///   <c>null</c>, defaults to <see cref="RelocationSummary.AllowRelocationInsideDestinationOnImport"/>.
    /// </param>
    /// <returns>
    ///   A stream of relocate results.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Preview")]
    public ActionResult<IEnumerable<ApiRelocationResult>> BatchPreviewFilesByScriptID(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchRelocatePreviewBody body,
        bool? move = null,
        bool? rename = null,
        bool? allowRelocationInsideDestination = null
    )
    {
        IRelocationPipe pipe;
        if (body is { ProviderID: null, Configuration: null or { Type: JTokenType.Null } })
        {
            if (relocationService.GetDefaultPipe() is not { ProviderInfo: { } } defaultPipe)
                return ValidationProblem("Default RelocationPipe not available or otherwise unusable.", nameof(body.ProviderID));

            pipe = defaultPipe;
        }
        else
        {
            if (!body.ProviderID.HasValue)
                return ValidationProblem("The ProviderID must be provided if a configuration object is provided.", nameof(body.ProviderID));

            if (relocationService.GetProviderInfo(body.ProviderID.Value) is not { } providerInfo)
                return ValidationProblem("The RelocationProvider with the given ID was not found.", nameof(body.ProviderID));

            if (providerInfo.ConfigurationInfo is not null)
            {
                if (body.Configuration is null or { Type: JTokenType.Null })
                    return ValidationProblem("The RelocationProvider expects a configuration object, and the provided configuration is not a valid JSON object.", nameof(body.Configuration));

                var data = body.Configuration.ToJson();
                var validationErrors = configurationService.Validate(providerInfo.ConfigurationInfo, data);
                if (validationErrors.Count > 0)
                    return ValidationProblem(validationErrors, nameof(body.Configuration));
                pipe = new RelocationPipe(providerInfo.ID, Encoding.UTF8.GetBytes(data));
            }
            else
            {
                if (body.Configuration is not null and not { Type: JTokenType.Null })
                    return ValidationProblem("The RelocationProvider does not expect a configuration object.", nameof(body.Configuration));
                pipe = new RelocationPipe(providerInfo.ID, null);
            }
        }

        return Ok(InternalBatchPreviewFiles(
            body.FileIDs,
            pipe,
            move ?? relocationService.MoveOnImport,
            rename ?? relocationService.RenameOnImport,
            allowRelocationInsideDestination ?? relocationService.AllowRelocationInsideDestinationOnImport
        ));
    }

    private IEnumerable<ApiRelocationResult> InternalBatchPreviewFiles(IEnumerable<int> fileLocationIDs, IRelocationPipe config, bool move, bool rename, bool allowRelocationInsideDestination)
    {
        foreach (var videoID in fileLocationIDs)
        {
            if (videoService.GetVideoByID(videoID) is not VideoLocal video)
            {
                yield return new ApiRelocationResult
                {
                    FileID = videoID,
                    IsSuccess = false,
                    IsPreview = true,
                    ErrorMessage = $"Unable to find File with ID {videoID}",
                };
                continue;
            }

            var videoFile = video.FirstResolvedPlace;
            if (videoFile is null)
            {
                videoFile = video.FirstValidPlace;
                yield return new ApiRelocationResult
                {
                    FileID = videoID,
                    IsSuccess = false,
                    IsPreview = true,
                    ErrorMessage = videoFile is not null
                        ? $"Unable to find any resolvable File.Location for File with ID {videoID}. Found valid but non-resolvable File.Location \"{videoFile.Path}\" with ID {videoFile.ID}."
                        : $"Unable to find any resolvable File.Location for File with ID {videoID}.",
                };
                continue;
            }

            RelocationResponse response;
            try
            {
                response = ((RelocationService)relocationService).ProcessPipe(videoFile, config, move, rename, allowRelocationInsideDestination, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                response = RelocationResponse.FromError($"An error occurred while trying to find a new file location: {ex.Message}", ex);
            }

            yield return new ApiRelocationResult
            {
                FileID = videoID,
                FileLocationID = videoFile.ID,
                IsSuccess = response.Success,
                IsPreview = true,
                IsRelocated = response.Moved || response.Renamed,
                PipeName = config is StoredRelocationPipe stored ? stored.Name : null,
                AbsolutePath = response.AbsolutePath,
                ManagedFolderID = response.ManagedFolder?.ID,
                RelativePath = response.RelativePath,
                ErrorMessage = response.Error?.Message,
            };
        }
    }

    #endregion

    #region Relocate

    /// <summary>
    /// Relocate a batch of files using the default Config
    /// </summary>
    /// <param name="fileIDs">The files to relocate</param>
    /// <param name="deleteEmptyDirectories">Whether or not to delete empty directories</param>
    /// <param name="move">Whether or not to move the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.MoveOnImport`</param>
    /// <param name="rename">Whether or not to rename the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.RenameOnImport`</param>
    /// <param name="allowRelocationInsideDestination">Whether or not to allow relocation of files inside the destination. If <c>null</c>, defaults to `Settings.Plugins.Renamer.AllowRelocationInsideDestination`</param>
    /// <returns>A stream of relocation results.</returns>
    [Authorize("admin")]
    [HttpPost("Relocate")]
    public ActionResult<IAsyncEnumerable<ApiRelocationResult>> BatchRelocateFilesWithDefaultConfig(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs,
        [FromQuery] bool deleteEmptyDirectories = true,
        [FromQuery] bool? move = null,
        [FromQuery] bool? rename = null,
        [FromQuery] bool? allowRelocationInsideDestination = null
    )
    {
        if (relocationService.GetDefaultPipe() is not { ProviderInfo: { } } pipe)
            return ValidationProblem("Default RelocationPipe not available or otherwise unusable.");

        return Ok(InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
        {
            Pipe = pipe,
            DeleteEmptyDirectories = deleteEmptyDirectories,
            Move = move ?? relocationService.MoveOnImport,
            Rename = rename ?? relocationService.RenameOnImport,
            AllowRelocationInsideDestination = allowRelocationInsideDestination ?? relocationService.AllowRelocationInsideDestinationOnImport,
        }));
    }

    private async IAsyncEnumerable<ApiRelocationResult> InternalBatchRelocateFiles(IEnumerable<int> fileIDs, AutoRelocateRequest request)
    {
        var configName = request.Pipe is IStoredRelocationPipe stored ? stored.Name : null;
        foreach (var vlID in fileIDs)
        {
            if (videoService.GetVideoByID(vlID) is not VideoLocal vl)
            {
                yield return new ApiRelocationResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    PipeName = configName,
                    ErrorMessage = $"Unable to find File with ID {vlID}",
                };
                continue;
            }

            var vlp = vl.FirstResolvedPlace;
            if (vlp is null)
            {
                vlp = vl.FirstValidPlace;
                yield return new ApiRelocationResult
                {
                    FileID = vlID,
                    PipeName = configName,
                    IsSuccess = false,
                    ErrorMessage = vlp is not null
                        ? $"Unable to find any resolvable File.Location for File with ID {vlID}. Found valid but non-resolvable File.Location \"{vlp.Path}\" with ID {vlp.ID}."
                        : $"Unable to find any resolvable File.Location for File with ID {vlID}.",
                };
                continue;
            }

            // Store the old managed folder id and relative path for comparison.
            var oldFolderId = vlp.ManagedFolderID;
            var oldRelativePath = vlp.RelativePath;
            var result = await relocationService.AutoRelocateFile(vlp, request);
            if (!result.Success)
            {
                yield return new ApiRelocationResult
                {
                    FileID = vlp.VideoID,
                    FileLocationID = vlp.ID,
                    PipeName = configName,
                    IsSuccess = false,
                    ErrorMessage = result.Error.Message,
                };
                continue;
            }

            RelocationResponse? otherResult = null;
            foreach (var otherVlp in vl.Places.Where(p => !string.IsNullOrEmpty(p?.Path) && System.IO.File.Exists(p.Path)))
            {
                if (otherVlp.ID == vlp.ID)
                    continue;

                otherResult = await relocationService.AutoRelocateFile(otherVlp, request);
                if (!otherResult.Success)
                    break;
            }
            if (otherResult is not null && !otherResult.Success)
            {
                yield return new ApiRelocationResult
                {
                    FileID = vlp.VideoID,
                    FileLocationID = vlp.ID,
                    PipeName = configName,
                    IsSuccess = false,
                    ErrorMessage = otherResult.Error.Message,
                };
                continue;
            }

            // Check if it was actually relocated, or if we landed on the same location as earlier.
            var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldFolderId != result.ManagedFolder.ID;
            yield return new ApiRelocationResult
            {
                FileID = vlp.VideoID,
                FileLocationID = vlp.ID,
                ManagedFolderID = result.ManagedFolder.ID,
                PipeName = configName,
                IsSuccess = true,
                IsRelocated = relocated,
                RelativePath = result.RelativePath,
                AbsolutePath = result.AbsolutePath
            };
        }
    }

    #endregion

    #region Pipes

    /// <summary>
    /// Get a list of all Configs
    /// </summary>
    /// <returns></returns>
    [HttpGet("Pipe")]
    public ActionResult<List<ApiRelocationPipe>> GetAllRelocationPipes()
        => relocationService.GetStoredPipes()
            .Select(pipeInfo => new ApiRelocationPipe(pipeInfo, pipeInfo.ProviderInfo))
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get the Config by the given Name
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <returns></returns>
    [HttpGet("Pipe/{pipeID}")]
    public ActionResult<ApiRelocationPipe> GetRenamerConfig([FromRoute] Guid pipeID)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        return new ApiRelocationPipe(pipeInfo, pipeInfo.ProviderInfo);
    }

    /// <summary>
    /// Create a new Config
    /// </summary>
    /// <param name="body">Config</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Pipe")]
    public ActionResult<ApiRelocationPipe> PostRenamerConfig([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] CreateRelocationPipeBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required");
        if (relocationService.GetProviderInfo(body.ProviderID) is not { } providerInfo)
            return NotFound("The RelocationProvider with the given ID was not found.");

        IRelocationProviderConfiguration? configuration = null;
        if (providerInfo.ConfigurationInfo is not null)
        {
            if (body.Configuration is not null)
            {
                if (body.Configuration is not { Type: JTokenType.Object })
                    return ValidationProblem("The provided configuration is not a valid JSON object or null.", nameof(body.Configuration));

                try
                {
                    var data = body.Configuration.ToJson();
                    var validationProblems = configurationService.Validate(providerInfo.ConfigurationInfo, data);
                    if (validationProblems.Count > 0)
                        return ValidationProblem(validationProblems, nameof(body.Configuration));

                    configuration = (IRelocationProviderConfiguration)configurationService.Deserialize(providerInfo.ConfigurationInfo, data);
                }
                catch (Exception ex)
                {
                    return ValidationProblem(ex.Message, nameof(body.Configuration));
                }
            }
            else
            {
                configuration = (IRelocationProviderConfiguration)configurationService.New(providerInfo.ConfigurationInfo);
            }
        }
        var pipeInfo = relocationService.StorePipe(providerInfo.Provider, body.Name, configuration, body.IsDefault);

        return new ApiRelocationPipe(pipeInfo, pipeInfo.ProviderInfo);
    }

    /// <summary>
    /// Update the Config by the given Name
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <param name="body">Config</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Pipe/{pipeID}")]
    public ActionResult<ApiRelocationPipe> PutRenamerConfig([FromRoute] Guid pipeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ModifyRelocationPipeBody body)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        var updated = false;
        if (!string.IsNullOrEmpty(body.Name) && pipeInfo.Name != body.Name)
        {
            pipeInfo.Name = body.Name;
            updated = false;
        }
        if (body.IsDefault.HasValue && pipeInfo.IsDefault != body.IsDefault.Value)
        {
            pipeInfo.IsDefault = body.IsDefault.Value;
            updated = true;
        }
        if (updated)
            relocationService.UpdatePipe(pipeInfo);

        return new ApiRelocationPipe(pipeInfo, pipeInfo.ProviderInfo);
    }

    /// <summary>
    /// Applies a JSON patch document to modify the Config with the given Name
    /// </summary>
    /// <param name="pipeID">
    /// The name of the config to be patched.
    /// </param>
    /// <param name="patchDocument">
    /// A JSON Patch document containing the modifications to be applied to the config.
    /// </param>
    /// <returns>
    /// The modified config if the operation is successful, or an error
    /// response if the config is not found, the patch document is invalid, or
    /// the modifications fail.
    /// </returns>
    [Authorize("admin")]
    [HttpPatch("Pipe/{pipeID}")]
    public ActionResult<ApiRelocationPipe> PatchRenamer([FromRoute] Guid pipeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ModifyRelocationPipeBody> patchDocument)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        var body = new ModifyRelocationPipeBody() { Name = pipeInfo.Name, IsDefault = pipeInfo.IsDefault };
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return PutRenamerConfig(pipeID, body);
    }

    /// <summary>
    /// Delete the Config by the given Name
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Pipe/{pipeID}")]
    public ActionResult DeleteRenamerConfig([FromRoute] Guid pipeID)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        if (pipeInfo.IsDefault)
            return BadRequest("The default relocation pipe cannot be deleted.");

        relocationService.DeletePipe(pipeInfo);

        return Ok();
    }

    /// <summary>
    /// Get the relocation provider by the given pipe ID
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID</param>
    /// <returns></returns>
    [HttpGet("Pipe/{pipeID}/Provider")]
    public ActionResult<RelocationProvider> GetRelocationProviderByPipeID([FromRoute] Guid pipeID)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        if (pipeInfo.ProviderInfo is not { } providerInfo)
            return NotFound("Relocation provider not found for relocation pipe.");

        return new RelocationProvider(providerInfo);
    }

    #region Pipes | Preview

    /// <summary>
    /// Preview the changes made by an existing Config, by the given Config Name
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <param name="fileIDs">The file IDs to preview</param>
    /// <param name="move">Whether or not to get the destination of the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.MoveOnImport`</param>
    /// <param name="rename">Whether or not to get the new name of the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.RenameOnImport`</param>
    /// <param name="allowRelocationInsideDestination">Whether or not to allow relocation of files inside the destination. If <c>null</c>, defaults to `Settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport`</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Pipe/{pipeID}/Preview")]
    public ActionResult<IEnumerable<ApiRelocationResult>> BatchRelocateFilesByScriptID(
        [FromRoute] Guid pipeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs,
        bool? move = null,
        bool? rename = null,
        bool? allowRelocationInsideDestination = null
    )
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        return Ok(InternalBatchPreviewFiles(
            fileIDs,
            pipeInfo,
            move ?? relocationService.MoveOnImport,
            rename ?? relocationService.RenameOnImport,
            allowRelocationInsideDestination ?? relocationService.AllowRelocationInsideDestinationOnImport
        ));
    }

    #endregion

    #region Pipes | Relocate

    /// <summary>
    /// Relocate a batch of files using a Config of the given name
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <param name="fileIDs">The files to relocate</param>
    /// <param name="deleteEmptyDirectories">Whether or not to delete empty directories</param>
    /// <param name="move">Whether or not to move the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.MoveOnImport`</param>
    /// <param name="rename">Whether or not to rename the files. If <c>null</c>, defaults to `Settings.Plugins.Renamer.RenameOnImport`</param>
    /// <param name="allowRelocationInsideDestination">Whether or not to allow relocation of files inside the destination. If <c>null</c>, defaults to `Settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport`</param>
    /// <returns>A stream of relocation results.</returns>
    [Authorize("admin")]
    [HttpPost("Pipe/{pipeID}/Relocate")]
    public ActionResult<IAsyncEnumerable<ApiRelocationResult>> BatchRelocateFilesByConfig(
        [FromRoute] Guid pipeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs,
        [FromQuery] bool deleteEmptyDirectories = true,
        [FromQuery] bool? move = null,
        [FromQuery] bool? rename = null,
        [FromQuery] bool? allowRelocationInsideDestination = null
    )
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        return Ok(
            InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
            {
                Pipe = pipeInfo,
                DeleteEmptyDirectories = deleteEmptyDirectories,
                Move = move ?? relocationService.MoveOnImport,
                Rename = rename ?? relocationService.RenameOnImport,
                AllowRelocationInsideDestination = allowRelocationInsideDestination ?? relocationService.AllowRelocationInsideDestinationOnImport,
            })
        );
    }

    #endregion

    #region Relocation | Configuration

    /// <summary>
    /// Get the current configuration for the relocation pipe with the given id.
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <returns></returns>
    [Produces("application/json")]
    [HttpGet("Pipe/{pipeID}/Configuration")]
    public ActionResult GetConfiguration(Guid pipeID)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        if (pipeInfo.ProviderInfo is not { } providerInfo)
        {
            if (pipeInfo.Configuration is null)
                return NotFound("Relocation provider not found for relocation pipe.");

            // Support showing the configuration in the REST API even if the provider is unavailable.
            return Content(Encoding.UTF8.GetString(pipeInfo.Configuration!), "application/json");
        }

        if (providerInfo.ConfigurationInfo is null)
            return NotFound("Relocation provider does not support configuration.");

        try
        {
            var config = pipeInfo.LoadConfiguration();
            return Content(configurationService.Serialize(config), "application/json");
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Overwrite the contents of the configuration for the relocation pipe with the given id.
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <param name="body">Configuration data</param>
    /// <returns></returns>
    [HttpPut("Pipe/{pipeID}/Configuration")]
    public ActionResult UpdateConfiguration(Guid pipeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken? body)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        try
        {
            var json = body is null or { Type: JTokenType.Null } ? null : body.ToString(Newtonsoft.Json.Formatting.None, [new StringEnumConverter()]);
            pipeInfo.SaveConfiguration(json);

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Patches the configuration for the relocation pipe with the given id using a JSON patch document.
    /// </summary>
    /// <param name="pipeID">Relocation pipe ID.</param>
    /// <param name="patchDocument">JSON patch document with operations to apply.</param>
    /// <returns></returns>
    [HttpPatch("Pipe/{pipeID}/Configuration")]
    public ActionResult UpdateConfiguration(Guid pipeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument patchDocument)
    {
        if (relocationService.GetStoredPipe(pipeID) is not { } pipeInfo)
            return NotFound("Relocation pipe not found");

        try
        {
            var config = pipeInfo.LoadConfiguration();
            patchDocument.ApplyTo(config);

            pipeInfo.SaveConfiguration(config);

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    #endregion

    #endregion
}
