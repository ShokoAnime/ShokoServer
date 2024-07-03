using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using ApiRenamer = Shoko.Server.API.v3.Models.Shoko.Renamer;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

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

    private readonly RenamerInstanceRepository _renamerInstanceRepository;
    private readonly RenameFileService _renameFileService;

    public RenamerController(ISettingsProvider settingsProvider, VideoLocal_PlaceService vlpService, VideoLocalRepository vlRepository, RenamerInstanceRepository renamerInstanceRepository, RenameFileService renameFileService) : base(settingsProvider)
    {
        _vlpService = vlpService;
        _vlRepository = vlRepository;
        _renamerInstanceRepository = renamerInstanceRepository;
        _renameFileService = renameFileService;
    }

    /// <summary>
    /// Get a list of all <see cref="Shoko.Server.API.v3.Models.Shoko.Renamer"/>s.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ApiRenamer>> GetAllRenamers()
    {
        return _renameFileService.AllRenamers.Select(a => GetRenamer(a.Key, a.Value)).ToList();
    }

    private static ApiRenamer GetRenamer(IBaseRenamer renamer, bool enabled)
    {
        // we can suppress nullability, because we check this when loading
        var attribute = renamer.GetType().GetCustomAttributes<RenamerIDAttribute>().FirstOrDefault()!;
        var settingsType = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        var settings = new List<ApiRenamer.RenamerSetting>();
        if (settingsType == null)
            return new ApiRenamer
            {
                RenamerID = attribute.RenamerId,
                Name = renamer.Name,
                Description = renamer.Description,
                Version = renamer.GetType().Assembly.GetName().Version?.ToString(),
                Enabled = enabled,
                Settings = settings
            };

        // settings
        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var renamerSettingAttribute = property.GetCustomAttribute<RenamerSettingAttribute>();
            var settingType = renamerSettingAttribute?.Type ?? RenamerSettingType.Auto;
            if (settingType == RenamerSettingType.Auto)
            {
                // we can't use a switch statement, because typeof() is not supported as a constant
                if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                    settingType = RenamerSettingType.Boolean;
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                    settingType = RenamerSettingType.Integer;
                else if (property.PropertyType == typeof(string))
                    settingType = RenamerSettingType.Text;
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                    settingType = RenamerSettingType.Decimal;
            }

            settings.Add(new ApiRenamer.RenamerSetting
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Type = property.PropertyType.Name,
                Description = renamerSettingAttribute?.Description,
                Language = renamerSettingAttribute?.Language,
                SettingType = settingType
            });
        }

        return new ApiRenamer
        {
            RenamerID = attribute.RenamerId,
            Name = renamer.Name,
            Description = renamer.Description,
            Version = renamer.GetType().Assembly.GetName().Version?.ToString(),
            Enabled = enabled,
            Settings = settings
        };
    }

    /// <summary>
    /// Get a list of all <see cref="RenamerInstance"/>s.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Instance")]
    public ActionResult<List<RenamerInstance>> GetAllRenamerInstances()
    {
        return _renamerInstanceRepository.GetAll().Select(GetRenamerInstance).ToList();
    }

    private static RenamerInstance GetRenamerInstance(Shoko.Server.Models.RenamerInstance p)
    {
        // we can suppress nullability, because we check this when loading
        var attribute = p.Type.GetCustomAttributes<RenamerIDAttribute>().FirstOrDefault()!;
        var settingsType = p.Type.GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        var settings = new List<RenamerInstance.RenamerSetting>();
        if (settingsType == null)
            return new RenamerInstance { RenamerID = attribute.RenamerId, Name = p.Name, Settings = settings };

        // settings
        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var renamerSettingAttribute = property.GetCustomAttribute<RenamerSettingAttribute>();
            settings.Add(new RenamerInstance.RenamerSetting
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Type = property.PropertyType.Name,
                Value = property.GetValue(p.Settings)
            });
        }

        return new RenamerInstance
        {
            RenamerID = attribute.RenamerId, Name = p.Name, Settings = settings
        };
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer"/> by the given <see cref="Shoko.Server.API.v3.Models.Shoko.RenamerInstance"/>.<see cref="Shoko.Server.API.v3.Models.Shoko.RenamerInstance.Name"/>
    /// </summary>
    /// <param name="renamerName">RenamerInstance Name</param>
    /// <returns></returns>
    [HttpGet("Instance/{renamerName}/Renamer")]
    public ActionResult<ApiRenamer> GetRenamerFromInstance([FromRoute] string renamerName)
    {
        var renamerInstance = _renamerInstanceRepository.GetByName(renamerName);
        if (renamerInstance == null)
            return NotFound("RenamerInstance not found");
        if (!_renameFileService.RenamersByType.TryGetValue(renamerInstance.Type, out var value))
            return NotFound("Renamer not found");

        return GetRenamer(value, true);
    }

    /// <summary>
    /// Get the <see cref="ApiRenamer"/> by the given <see cref="Shoko.Server.API.v3.Models.Shoko.Renamer"/>.<see cref="Renamer.RenamerID"/>
    /// </summary>
    /// <param name="renamerID">RenamerID</param>
    /// <returns></returns>
    [HttpGet("{renamerID}")]
    public ActionResult<ApiRenamer> GetRenamer([FromRoute] string renamerID)
    {
        if (!_renameFileService.RenamersByKey.TryGetValue(renamerID, out var value))
            return NotFound("Renamer not found");

        return GetRenamer(value, true);
    }

    /// <summary>
    /// Get the <see cref="RenamerInstance"/> by the given <see cref="RenamerInstance"/>.<see cref="RenamerInstance.Name"/>
    /// </summary>
    /// <param name="renamerName">RenamerInstance Name</param>
    /// <returns></returns>
    [HttpGet("Instance/{renamerName}")]
    public ActionResult<RenamerInstance> GetRenamerInstance([FromRoute] string renamerName)
    {
        var instance = _renamerInstanceRepository.GetByName(renamerName);
        if (instance == null)
            return NotFound("RenamerInstance not found");

        return GetRenamerInstance(instance);
    }

    /// <summary>
    /// Create a new <see cref="Shoko.Server.API.v3.Models.Shoko.RenamerInstance"/>.
    /// </summary>
    /// <param name="body">RenamerInstance</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Instance")]
    public ActionResult<RenamerInstance> PostRenamerInstance([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RenamerInstance body)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required");
        if (!_renameFileService.RenamersByKey.TryGetValue(body.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        var renamerInstance = new Shoko.Server.Models.RenamerInstance
        {
            Name = body.Name,
            Type = renamer.GetType(),
        };

        if (body.Settings != null)
        {
            var properties = renamerInstance.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var setting in body.Settings)
            {
                var property = properties.FirstOrDefault(x => x.Name == setting.Name) ?? properties.FirstOrDefault(x => x.GetCustomAttribute<RenamerSettingAttribute>()?.Name == setting.Name);

                if (property == null)
                    continue;

                property.SetValue(renamerInstance.Settings, Convert.ChangeType(setting.Value, property.PropertyType));
            }
        }

        _renamerInstanceRepository.Save(renamerInstance);

        return GetRenamerInstance(renamerInstance);
    }

    /// <summary>
    /// Update the <see cref="RenamerInstance"/> by the given <see cref="RenamerInstance"/>.<see cref="RenamerInstance.Name"/>
    /// </summary>
    /// <param name="renamerName">RenamerInstance Name</param>
    /// <param name="body">RenamerInstance</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Instance/{renamerName}")]
    public ActionResult<RenamerInstance> PutRenamerInstance([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RenamerInstance body)
    {
        var renamerInstance = _renamerInstanceRepository.GetByName(renamerName);
        if (renamerInstance == null)
            return NotFound("RenamerInstance not found");

        if (!_renameFileService.RenamersByKey.TryGetValue(body.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        renamerInstance.Type = renamer.GetType();

        if (body.Settings != null)
        {
            var properties = renamerInstance.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var setting in body.Settings)
            {
                var property = properties.FirstOrDefault(x => x.Name == setting.Name) ?? properties.FirstOrDefault(x => x.GetCustomAttribute<RenamerSettingAttribute>()?.Name == setting.Name);

                if (property == null)
                    continue;

                property.SetValue(renamerInstance.Settings, Convert.ChangeType(setting.Value, property.PropertyType));
            }
        }

        _renamerInstanceRepository.Save(renamerInstance);

        return GetRenamerInstance(renamerInstance);
    }

    /*
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
        if (!RenameFileService.Renamer.TryGetValue(renamerName, out var value))
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
