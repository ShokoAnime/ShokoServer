using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Server.API.Annotations;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using ApiRenamer = Shoko.Server.API.v3.Models.Shoko.Renamer;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RenamerController : BaseController
{
    private readonly VideoLocal_PlaceService _vlpService;

    private readonly VideoLocalRepository _vlRepository;

    private readonly RenamerInstanceRepository _rsRepository;
    private readonly RenameFileService _renameFileService;

    public RenamerController(ISettingsProvider settingsProvider, VideoLocal_PlaceService vlpService, VideoLocalRepository vlRepository, RenamerInstanceRepository rsRepository, RenameFileService renameFileService) : base(settingsProvider)
    {
        _vlpService = vlpService;
        _vlRepository = vlRepository;
        _rsRepository = rsRepository;
        _renameFileService = renameFileService;
    }
    
    /// <summary>
    /// Get a list of all <see cref="ApiRenamer"/>s.
    /// </summary>
    /// <returns></returns>
    /*[HttpGet]
    public ActionResult<List<ApiRenamer>> GetAllRenamers()
    {
        return _renameFileService.Renamers
            .Select(p => new ApiRenamer(p.Key, p.Value))
            .ToList();
    }

    /// <summary>
    /// Preview batch changes to files.
    /// </summary>
    /// <param name="body">Contains the files, renamer and script to use for the preview.</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Preview")]
    public ActionResult<IAsyncEnumerable<ApiRenamer.RelocateResult>> BatchPreviewRelocateFiles([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.BatchPreviewAutoRelocateWithRenamerBody body)
    {
        if (!RenameFileService.Renamers.ContainsKey(body.RenamerName))
            ModelState.AddModelError(nameof(body.RenamerName), "Renamer not found.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return new ActionResult<IAsyncEnumerable<ApiRenamer.RelocateResult>>(
            InternalBatchRelocateFiles(body.FileIDs, new() { RenamerName = body.RenamerName, Settings = body.ScriptBody, Preview = true, Move = body.Move })
        );
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer"/> by the given <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">Renamer ID</param>
    /// <returns></returns>
    [HttpGet("{renamerName}")]
    public ActionResult<ApiRenamer> GetRenamer([FromRoute] string renamerName)
    {
        if (!RenameFileService.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        return new ApiRenamer(renamerName, value);
    }

    /// <summary>
    /// Modifies the settings of the <see cref="ApiRenamer"/> with the
    /// given /// <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">
    /// The name of the renamer to be updated.
    /// </param>
    /// <param name="body">
    /// An object containing the modifications to be applied to the renamer.
    /// </param>
    /// <returns>
    /// The modified renamer if the operation is successful, or an error
    /// response if the renamer is not found or the modification fails.
    /// </returns>
    [Authorize("admin")]
    [HttpPut("{renamerName}")]
    public ActionResult<ApiRenamer> PutRenamer([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.ModifyRenamerBody body)
    {
        if (!RenameFileService.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        return body.MergeWithExisting(renamerName, value);
    }

    /// <summary>
    /// Applies a JSON patch document to modify the settings of the
    /// <see cref="ApiRenamer"/> with the given
    /// <paramref name="renamerName"/>.
    /// </summary>
    /// <param name="renamerName">
    /// The name of the renamer to be patched.
    /// </param>
    /// <param name="patchDocument">
    /// A JSON Patch document containing the modifications to be applied to the
    /// renamer.
    /// </param>
    /// <returns>
    /// The modified renamer if the operation is successful, or an error
    /// response if the renamer is not found, the patch document is invalid, or
    /// the modifications fail.
    /// </returns>
    [Authorize("admin")]
    [HttpPatch("{renamerName}")]
    public ActionResult<ApiRenamer> PatchRenamer([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ApiRenamer.Input.ModifyRenamerBody> patchDocument)
    {
        if (!RenameFileService.Renamers.TryGetValue(renamerName, out var value))
            return NotFound("Renamer not found.");

        // Patch the renamer in the v3 model and merge it back into the
        // settings.
        var modifyRenamer = new ApiRenamer.Input.ModifyRenamerBody(renamerName);
        patchDocument.ApplyTo(modifyRenamer, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return modifyRenamer.MergeWithExisting(renamerName, value);
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer.Script"/>s for all or a single renamer.
    /// </summary>
    /// <param name="renamerName">Renamer ID</param>
    /// <returns>The scripts.</returns>
    [HttpGet("Script")]
    public ActionResult<List<ApiRenamer.Script>> GetAllRenamerScripts([FromQuery] string? renamerName = null)
    {
        if (!string.IsNullOrEmpty(renamerName))
        {
            if (!RenameFileService.Renamers.ContainsKey(renamerName))
                return new List<ApiRenamer.Script>();

            return _rsRepository.GetByType(renamerName)
                .Where(s => s.ScriptName != Shoko.Models.Constants.Renamer.TempFileName)
                .Select(s => new ApiRenamer.Script(s))
                .OrderBy(s => s.ID)
                .ToList();
        }

        return _rsRepository.GetAll()
            .Where(s => s.ScriptName != Shoko.Models.Constants.Renamer.TempFileName)
            .Select(s => new ApiRenamer.Script(s))
            .OrderBy(s => s.ID)
            .ToList();
    }

    /// <summary>
    /// Add a new script.
    /// </summary>
    /// <param name="body">The script to add.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Script")]
    public ActionResult<ApiRenamer.Script> AddRenamerScript([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.NewScriptBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ValidationProblem("Script name cannot be empty.", nameof(body.Name));

        if (string.Equals(body.Name, Shoko.Models.Constants.Renamer.TempFileName))
            return ValidationProblem("Script name cannot be the same as the v1 temp script file.", nameof(body.Name));

        var script = _rsRepository.GetByName(body.Name);
        if (script is not null)
            return ValidationProblem("A script with the given name already exists!", nameof(body.Name));

        script = new Shoko.Models.Server.RenameScript
        {
            ScriptName = body.Name,
            RenamerType = body.RenamerName,
            IsEnabledOnImport = body.EnabledOnImport ? 1 : 0,
            Script = body.Body,
            ExtraData = null,
        };
        _rsRepository.Save(script);

        return Created($"/api/v3/Renamer/Script/{script.RenameScriptID}", new ApiRenamer.Script(script));
    }

    /// <summary>
    /// Get a <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <returns>The script</returns>
    [HttpGet("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> GetRenamerScriptByScriptID([FromRoute] int scriptID)
    {
        var script = scriptID is > 0 ? _rsRepository.GetByID(scriptID) : null;
        if (script is null || string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
            return NotFound("Renamer.Script not found.");

        return new ApiRenamer.Script(script);
    }

    /// <summary>
    /// Replace an existing <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <param name="modifyScript">The modified script to replace the existing script with.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> PutRenamerScriptByScriptID([FromRoute] int scriptID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.ModifyScriptBody modifyScript)
    {
        var script = scriptID is > 0 ? _rsRepository.GetByID(scriptID) : null;
        if (script is null || string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
            return NotFound("Renamer.Script not found.");

        if (string.IsNullOrWhiteSpace(modifyScript.Name))
            return ValidationProblem("Script name cannot be empty.", nameof(modifyScript.Name));

        // Guard against rename-collisions.
        if (modifyScript.Name != script.ScriptName)
        {
            var anotherScript = _rsRepository.GetByName(modifyScript.Name);
            if (anotherScript != null)
                return ValidationProblem("Another script with the given name already exists!", nameof(modifyScript.Name));
        }

        return modifyScript.MergeWithExisting(script);
    }

    /// <summary>
    /// Patch an existing <see cref="ApiRenamer.Script"/> by the given
    /// <paramref name="scriptID"/>.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <param name="patchDocument">The json patch document to update the script
    /// with.</param>
    /// <returns>The updated <see cref="ApiRenamer.Script"/></returns>
    [Authorize("admin")]
    [HttpPatch("Script/{scriptID}")]
    public ActionResult<ApiRenamer.Script> PatchRenamerScriptByScriptID([FromRoute] int scriptID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ApiRenamer.Input.ModifyScriptBody> patchDocument)
    {
        var script = scriptID is > 0 ? _rsRepository.GetByID(scriptID) : null;
        if (script is null || string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
            return NotFound("Renamer.Script not found.");

        // Patch the script in the v3 model and merge it back into the database
        // model.
        var modifyScript = new ApiRenamer.Input.ModifyScriptBody(script);
        patchDocument.ApplyTo(modifyScript, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(modifyScript.Name))
            return ValidationProblem("Script name cannot be empty.", nameof(modifyScript.Name));

        // Guard against rename-collisions.
        if (modifyScript.Name != script.ScriptName)
        {
            var anotherScript = _rsRepository.GetByName(modifyScript.Name);
            if (anotherScript != null)
                return ValidationProblem("Another script with the given name already exists!", nameof(modifyScript.Name));
        }

        return modifyScript.MergeWithExisting(script);
    }

    /// <summary>
    /// Delete an existing <see cref="ApiRenamer.Script"/> by the given <paramref name="scriptID"/>
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Script/{scriptID}")]
    public ActionResult DeleteRenamerScriptByScriptID([FromRoute] int scriptID)
    {
        var script = scriptID is > 0 ? _rsRepository.GetByID(scriptID) : null;
        if (script is null || string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
            return NotFound("Renamer.Script not found.");

        _rsRepository.Delete(script);

        return NoContent();
    }

    /// <summary>
    /// Execute the script and either preview the changes or commit the changes
    /// on a batch of files.
    /// </summary>
    /// <param name="scriptID">Script ID</param>
    /// <param name="body">Contains the files, renamer and script to use for the preview.</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Script/{scriptID}/Execute")]
    public ActionResult<IAsyncEnumerable<ApiRenamer.RelocateResult>> BatchRelocateFilesByScriptID([FromRoute] int scriptID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.BatchAutoRelocateBody body)
    {
        var script = scriptID is > 0 ? _rsRepository.GetByID(scriptID) : null;
        if (script is null || string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
            return NotFound("Renamer.Script not found.");

        if (!RenameFileService.Renamers.ContainsKey(script.RenamerType))
            return BadRequest("Renamer for Renamer.Script not found.");

        return new ActionResult<IAsyncEnumerable<ApiRenamer.RelocateResult>>(
            InternalBatchRelocateFiles(body.FileIDs, new() { DeleteEmptyDirectories = body.DeleteEmptyDirectories, Move = body.Move, Preview = body.Preview, ScriptID = scriptID })
        );
    }

    [NonAction]
    private async IAsyncEnumerable<ApiRenamer.RelocateResult> InternalBatchRelocateFiles(IEnumerable<int> fileIDs, AutoRelocateRequest request)
    {
        foreach (var vlID in fileIDs)
        {
            var vl = vlID is > 0 ? _vlRepository.GetByID(vlID) : null;
            if (vl is null)
            {
                yield return new()
                {
                    FileID = vlID,
                    IsSuccess = false,
                    ErrorMessage = $"Unable to find File with ID {vlID}",
                };
                continue;
            }

            var vlp = vl.FirstResolvedPlace;
            if (vlp is null)
            {
                vlp = vl.FirstValidPlace;
                yield return new()
                {
                    FileID = vlID,
                    IsSuccess = false,
                    ErrorMessage = vlp is not null
                        ? $"Unable to find any resolvable File.Location for File with ID {vlID}. Found valid but non-resolvable File.Location \"{vlp.FullServerPath}\" with ID {vlp.VideoLocal_Place_ID}."
                        : $"Unable to find any resolvable File.Location for File with ID {vlID}.",
                };
                continue;
            }

            // Store the old import folder id and relative path for comparison.
            var oldImportFolderId = vlp.ImportFolderID;
            var oldRelativePath = vlp.FilePath;
            var result = await _vlpService.AutoRelocateFile(vlp, request);
            if (!result.Success)
            {
                yield return new()
                {
                    FileID = vlp.VideoLocalID,
                    FileLocationID = vlp.VideoLocal_Place_ID,
                    IsSuccess = false,
                    ErrorMessage = result.ErrorMessage,
                };
                continue;
            }

            if (!request.Preview)
            {
                RelocationResult? otherResult = null;
                foreach (var otherVlp in vl.Places.Where(p => !string.IsNullOrEmpty(p?.FullServerPath) && System.IO.File.Exists(p.FullServerPath)))
                {
                    if (otherVlp.VideoLocal_Place_ID == vlp.VideoLocal_Place_ID)
                        continue;

                    otherResult = await _vlpService.AutoRelocateFile(otherVlp, request);
                    if (!otherResult.Success)
                        break;
                }
                if (otherResult is not null && !otherResult.Success)
                {
                    yield return new()
                    {
                        FileID = vlp.VideoLocalID,
                        FileLocationID = vlp.VideoLocal_Place_ID,
                        IsSuccess = false,
                        ErrorMessage = result.ErrorMessage,
                    };
                    continue;
                }
            }

            // Check if it was actually relocated, or if we landed on the same location as earlier.
            var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldImportFolderId != result.ImportFolder.ImportFolderID;
            yield return new()
            {
                FileID = vlp.VideoLocalID,
                FileLocationID = vlp.VideoLocal_Place_ID,
                ImportFolderID = result.ImportFolder.ImportFolderID,
                ScriptID = request.ScriptID,
                IsSuccess = true,
                IsRelocated = relocated,
                IsPreview = request.Preview,
                RelativePath = result.RelativePath,
                AbsolutePath = result.AbsolutePath,
            };
        }
    }*/
}
