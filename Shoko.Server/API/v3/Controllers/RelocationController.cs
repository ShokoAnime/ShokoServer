using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Relocation;
using Shoko.Server.API.v3.Models.Relocation.Input;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;

using ApiRelocationPreset = Shoko.Server.API.v3.Models.Relocation.RelocationPreset;
using ApiRelocationResult = Shoko.Server.API.v3.Models.Relocation.RelocationResult;
using RelocationPreset = Shoko.Abstractions.Video.Relocation.RelocationPreset;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
///   Controller responsible for handling file relocation. Interacts with the <see cref="IVideoRelocationService"/>.
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
/// <param name="presetManager">
///   Relocation preset manager.
/// </param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RelocationController(
    ISettingsProvider settingsProvider,
    IPluginManager pluginManager,
    IConfigurationService configurationService,
    IVideoService videoService,
    IVideoRelocationService relocationService,
    IRelocationPresetManager presetManager
) : BaseController(settingsProvider)
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
    public ActionResult<RelocationSummary> GetRelocationSummary()
        => new RelocationSummary
        {
            RenameOnImport = presetManager.RenameOnImport,
            MoveOnImport = presetManager.MoveOnImport,
            AllowRelocationInsideDestinationOnImport = presetManager.AllowRelocationInsideDestinationOnImport,
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
    public ActionResult UpdateRelocationSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] UpdateRelocationSettingsBody body)
    {
        if (body.RenameOnImport.HasValue)
            presetManager.RenameOnImport = body.RenameOnImport.Value;
        if (body.MoveOnImport.HasValue)
            presetManager.MoveOnImport = body.MoveOnImport.Value;
        if (body.AllowRelocationInsideDestinationOnImport.HasValue)
            presetManager.AllowRelocationInsideDestinationOnImport = body.AllowRelocationInsideDestinationOnImport.Value;

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
    public ActionResult<List<RelocationProvider>> GetAvailableRelocationProviders([FromQuery] Guid? pluginID = null)
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
    public ActionResult<RelocationProvider> GetRelocationProviderByProviderID([FromRoute] Guid providerID)
    {
        if (relocationService.GetProviderInfo(providerID) is not { } value)
            return NotFound("Renamer not found");

        return new RelocationProvider(value);
    }

    #endregion

    #region Preview

    /// <summary>
    ///   Preview running a relocation provider on a batch of files, using the
    ///   default preset or the provided provider identified by ID and provided
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
    public ActionResult<IEnumerable<ApiRelocationResult>> BatchPreviewFilesWithProviderAndConfig(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchRelocatePreviewBody body,
        bool? move = null,
        bool? rename = null,
        bool? allowRelocationInsideDestination = null
    )
    {
        IRelocationPreset preset;
        if (body is { ProviderID: null, Configuration: null or { Type: JTokenType.Null } })
        {
            if (presetManager.GetDefaultPreset() is not { ProviderInfo: { } } defaultPreset)
                return ValidationProblem("Default RelocationPreset not available or otherwise unusable.", nameof(body.ProviderID));

            preset = defaultPreset;
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
                preset = new RelocationPreset(providerInfo.ID, Encoding.UTF8.GetBytes(data));
            }
            else
            {
                if (body.Configuration is not null and not { Type: JTokenType.Null })
                    return ValidationProblem("The RelocationProvider does not expect a configuration object.", nameof(body.Configuration));
                preset = new RelocationPreset(providerInfo.ID, null);
            }
        }

        return Ok(InternalBatchPreviewFiles(
            body.FileIDs,
            preset,
            move ?? presetManager.MoveOnImport,
            rename ?? presetManager.RenameOnImport,
            allowRelocationInsideDestination ?? presetManager.AllowRelocationInsideDestinationOnImport
        ));
    }

    private IEnumerable<ApiRelocationResult> InternalBatchPreviewFiles(IEnumerable<int> fileLocationIDs, IRelocationPreset config, bool move, bool rename, bool allowRelocationInsideDestination)
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
                response = ((VideoRelocationService)relocationService).ProcessPreset(videoFile, config, move, rename, allowRelocationInsideDestination, HttpContext.RequestAborted);
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
                PresetName = config is StoredRelocationPreset stored ? stored.Name : null,
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
    ///   Run the default relocation preset on a batch of files.
    /// </summary>
    /// <param name="fileIDs">
    ///   The body with the file IDs to use.
    /// </param>
    /// <param name="deleteEmptyDirectories">
    ///   Whether or not to delete empty directories. Defaults to true.
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
    ///   A stream of <see cref="ApiRelocationResult"/>s.
    /// </returns>
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
        if (presetManager.GetDefaultPreset() is not { ProviderInfo: { } } preset)
            return ValidationProblem("Default RelocationPreset not available or otherwise unusable.");

        return Ok(InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
        {
            Preset = preset,
            DeleteEmptyDirectories = deleteEmptyDirectories && (move ?? presetManager.MoveOnImport),
            Move = move ?? presetManager.MoveOnImport,
            Rename = rename ?? presetManager.RenameOnImport,
            AllowRelocationInsideDestination = allowRelocationInsideDestination ?? presetManager.AllowRelocationInsideDestinationOnImport,
        }));
    }

    private async IAsyncEnumerable<ApiRelocationResult> InternalBatchRelocateFiles(IEnumerable<int> fileIDs, AutoRelocateRequest request)
    {
        var configName = request.Preset is IStoredRelocationPreset stored ? stored.Name : null;
        foreach (var vlID in fileIDs)
        {
            if (videoService.GetVideoByID(vlID) is not VideoLocal vl)
            {
                yield return new ApiRelocationResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    PresetName = configName,
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
                    PresetName = configName,
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
                    PresetName = configName,
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
                    PresetName = configName,
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
                PresetName = configName,
                IsSuccess = true,
                IsRelocated = relocated,
                RelativePath = result.RelativePath,
                AbsolutePath = result.AbsolutePath
            };
        }
    }

    #endregion

    #region Presets

    /// <summary>
    ///   Gets a list of all relocation presets.
    /// </summary>
    /// <returns>
    ///   A list of <see cref="ApiRelocationPreset"/>s.
    /// </returns>
    [HttpGet("Preset")]
    public ActionResult<List<ApiRelocationPreset>> GetAllRelocationPresets()
        => presetManager.GetStoredPresets()
            .Select(presetInfo => new ApiRelocationPreset(presetInfo, presetInfo.ProviderInfo))
            .WhereNotNull()
            .ToList();

    /// <summary>
    ///   Create a new relocation preset from the given body.
    /// </summary>
    /// <param name="body">
    ///   The details such as the name and provider of the preset to be created,
    ///   optionally with the configuration if the provider requires it, but
    ///   it can be left out to use a new configuration.
    /// </param>
    /// <returns>
    ///   The newly created <see cref="ApiRelocationPreset"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Preset")]
    public ActionResult<ApiRelocationPreset> NewRelocationPreset([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] CreateRelocationPresetBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required");
        if (relocationService.GetProviderInfo(body.ProviderID) is not { } providerInfo)
            return NotFound("The RelocationProvider with the given ID was not found.");

        IRelocationProviderConfiguration? configuration = null;
        if (providerInfo.ConfigurationInfo is not null)
        {
            if (body.Configuration is not null and not { Type: JTokenType.Null })
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
        var presetInfo = presetManager.StorePreset(providerInfo.Provider, body.Name, configuration, body.IsDefault);

        return new ApiRelocationPreset(presetInfo, presetInfo.ProviderInfo);
    }

    /// <summary>
    ///   Get the relocation preset by the given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <returns>
    ///   The <see cref="ApiRelocationPreset"/>.
    /// </returns>
    [HttpGet("Preset/{presetID}")]
    public ActionResult<ApiRelocationPreset> GetRelocationPresetByPresetID([FromRoute] Guid presetID)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        return new ApiRelocationPreset(presetInfo, presetInfo.ProviderInfo);
    }

    /// <summary>
    ///   Modify the relocation preset by the given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///    Relocation preset ID.
    /// </param>
    /// <param name="body">
    ///   The details for what to update.
    /// </param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Preset/{presetID}")]
    public ActionResult<ApiRelocationPreset> PutRelocationPresetByPresetID([FromRoute] Guid presetID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ModifyRelocationPresetBody body)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        var updated = false;
        if (!string.IsNullOrEmpty(body.Name) && presetInfo.Name != body.Name)
        {
            presetInfo.Name = body.Name;
            updated = true;
        }
        if (body.IsDefault.HasValue && presetInfo.IsDefault != body.IsDefault.Value)
        {
            presetInfo.IsDefault = body.IsDefault.Value;
            updated = true;
        }
        if (updated)
            presetManager.UpdatePreset(presetInfo);

        return new ApiRelocationPreset(presetInfo, presetInfo.ProviderInfo);
    }

    /// <summary>
    ///   Applies a JSON patch document to modify the relocation preset by the
    ///   given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <param name="patchDocument">
    ///   A JSON Patch document containing the modifications to be applied to
    ///   the relocation preset.
    /// </param>
    /// <returns>
    ///   The newly updated <see cref="ApiRelocationPreset"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPatch("Preset/{presetID}")]
    public ActionResult<ApiRelocationPreset> PatchRelocationPresetByPresetID([FromRoute] Guid presetID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ModifyRelocationPresetBody> patchDocument)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        var body = new ModifyRelocationPresetBody() { Name = presetInfo.Name, IsDefault = presetInfo.IsDefault };
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return PutRelocationPresetByPresetID(presetID, body);
    }

    /// <summary>
    /// Delete the relocation preset by the given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <returns>
    ///   No content.
    /// </returns>
    [Authorize("admin")]
    [HttpDelete("Preset/{presetID}")]
    public ActionResult DeleteRelocationPresetByPresetID([FromRoute] Guid presetID)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        if (presetInfo.IsDefault)
            return BadRequest("The default relocation preset cannot be deleted.");

        presetManager.DeletePreset(presetInfo);

        return NoContent();
    }

    /// <summary>
    ///   Get the relocation provider by the given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <returns>
    ///   The <see cref="RelocationProvider"/> for the preset.
    /// </returns>
    [HttpGet("Preset/{presetID}/Provider")]
    public ActionResult<RelocationProvider> GetRelocationProviderByPresetID([FromRoute] Guid presetID)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        if (presetInfo.ProviderInfo is not { } providerInfo)
            return NotFound("Relocation provider not found for relocation preset.");

        return new RelocationProvider(providerInfo);
    }

    #region Presets | Preview

    /// <summary>
    ///   Preview what would happen if you were to apply the relocation preset by
    ///   the given preset ID to the given files.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <param name="fileIDs">
    ///   The file IDs to preview.
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
    ///   A stream of <see cref="ApiRelocationResult"/>s.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Preset/{presetID}/Preview")]
    public ActionResult<IEnumerable<ApiRelocationResult>> BatchRelocateFilesByScriptID(
        [FromRoute] Guid presetID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs,
        bool? move = null,
        bool? rename = null,
        bool? allowRelocationInsideDestination = null
    )
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        return Ok(InternalBatchPreviewFiles(
            fileIDs,
            presetInfo,
            move ?? presetManager.MoveOnImport,
            rename ?? presetManager.RenameOnImport,
            allowRelocationInsideDestination ?? presetManager.AllowRelocationInsideDestinationOnImport
        ));
    }

    #endregion

    #region Presets | Relocate

    /// <summary>
    ///   Relocate the files with the relocation preset by the given preset ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <param name="fileIDs">
    ///   The file IDs to relocate.
    /// </param>
    /// <param name="deleteEmptyDirectories">
    ///   Whether or not to delete empty directories. Defaults to true.
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
    ///   A stream of <see cref="ApiRelocationResult"/>s.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Preset/{presetID}/Relocate")]
    public ActionResult<IAsyncEnumerable<ApiRelocationResult>> BatchRelocateFilesByConfig(
        [FromRoute] Guid presetID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs,
        [FromQuery] bool deleteEmptyDirectories = true,
        [FromQuery] bool? move = null,
        [FromQuery] bool? rename = null,
        [FromQuery] bool? allowRelocationInsideDestination = null
    )
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        return Ok(
            InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
            {
                Preset = presetInfo,
                DeleteEmptyDirectories = deleteEmptyDirectories,
                Move = move ?? presetManager.MoveOnImport,
                Rename = rename ?? presetManager.RenameOnImport,
                AllowRelocationInsideDestination = allowRelocationInsideDestination ?? presetManager.AllowRelocationInsideDestinationOnImport,
            })
        );
    }

    #endregion

    #region Relocation | Configuration

    /// <summary>
    ///   Get the current configuration for the relocation preset with the given
    ///   ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <returns>
    ///   The current configuration for the relocation preset.
    /// </returns>
    [Produces("application/json")]
    [HttpGet("Preset/{presetID}/Configuration")]
    public ActionResult GetConfigurationForRelocationPresetByPresetID(Guid presetID)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        if (presetInfo.ProviderInfo is not { } providerInfo)
        {
            if (presetInfo.Configuration is null)
                return NotFound("Relocation provider not found for relocation preset.");

            // Support showing the configuration in the REST API even if the provider is unavailable.
            return Content(Encoding.UTF8.GetString(presetInfo.Configuration!), "application/json");
        }

        if (providerInfo.ConfigurationInfo is null)
            return NotFound("Relocation provider does not support configuration.");

        try
        {
            var config = presetInfo.LoadConfiguration();
            return Content(configurationService.Serialize(config), "application/json");
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    ///   Overwrite the contents of the configuration for the relocation preset
    ///   with the given ID.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <param name="body">
    ///   The new configuration.
    /// </param>
    /// <returns>
    ///   Ok if successful.
    /// </returns>
    [HttpPut("Preset/{presetID}/Configuration")]
    public ActionResult PutConfigurationForRelocationPresetByPresetID(Guid presetID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken? body)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        try
        {
            var json = body is null or { Type: JTokenType.Null } ? null : body.ToString(Formatting.None, [new StringEnumConverter()]);
            presetInfo.SaveConfiguration(json);

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    ///   Patches the configuration for the relocation preset with the given ID
    ///   using a JSON patch document.
    /// </summary>
    /// <param name="presetID">
    ///   Relocation preset ID.
    /// </param>
    /// <param name="patchDocument">
    ///   JSON patch document with operations to apply.
    /// </param>
    /// <returns>
    ///   Ok if successful.
    /// </returns>
    [HttpPatch("Preset/{presetID}/Configuration")]
    public ActionResult PatchConfigurationForRelocationPresetByPresetID(Guid presetID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument patchDocument)
    {
        if (presetManager.GetStoredPreset(presetID) is not { } presetInfo)
            return NotFound("Relocation preset not found");

        try
        {
            var config = presetInfo.LoadConfiguration();
            patchDocument.ApplyTo(config);

            presetInfo.SaveConfiguration(config);

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
