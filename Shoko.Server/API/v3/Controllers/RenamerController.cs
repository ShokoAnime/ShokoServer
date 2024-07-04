using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Force.DeepCloner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
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

    private readonly RenamerConfigRepository _renamerConfigRepository;
    private readonly RenameFileService _renameFileService;
    private ISettingsProvider _settingsProvider;

    public RenamerController(ISettingsProvider settingsProvider, VideoLocal_PlaceService vlpService, VideoLocalRepository vlRepository, RenamerConfigRepository renamerConfigRepository, RenameFileService renameFileService) : base(settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _vlpService = vlpService;
        _vlRepository = vlRepository;
        _renamerConfigRepository = renamerConfigRepository;
        _renameFileService = renameFileService;
    }

    /// <summary>
    /// Get a list of all Renamers.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ApiRenamer>> GetAllRenamers()
    {
        return _renameFileService.AllRenamers.Select(a => GetRenamer(a.Key, a.Value)).ToList();
    }

    /// <summary>
    /// Get the Renamer by the given RenamerID
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

    private static ApiRenamer GetRenamer(IBaseRenamer renamer, bool enabled)
    {
        // we can suppress nullability, because we check this when loading
        var attribute = renamer.GetType().GetCustomAttributes<RenamerIDAttribute>().FirstOrDefault()!;
        var settingsType = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        var settings = new List<ApiRenamer.Setting>();
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

            settings.Add(new ApiRenamer.Setting
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
    /// Get a list of all Configs
    /// </summary>
    /// <returns></returns>
    [HttpGet("Config")]
    public ActionResult<List<RenamerConfig>> GetAllRenamerConfigs()
    {
        return _renamerConfigRepository.GetAll().Select(GetRenamerConfig).ToList();
    }

    private static RenamerConfig GetRenamerConfig(Shoko.Server.Models.RenamerConfig p)
    {
        // we can suppress nullability, because we check this when loading
        var attribute = p.Type.GetCustomAttributes<RenamerIDAttribute>().FirstOrDefault()!;
        var settingsType = p.Type.GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        var settings = new List<RenamerConfig.RenamerSetting>();
        if (settingsType == null)
            return new RenamerConfig { RenamerID = attribute.RenamerId, Name = p.Name, Settings = settings };

        // settings
        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var renamerSettingAttribute = property.GetCustomAttribute<RenamerSettingAttribute>();
            settings.Add(new RenamerConfig.RenamerSetting
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Type = property.PropertyType.Name,
                Value = property.GetValue(p.Settings)
            });
        }

        return new RenamerConfig
        {
            RenamerID = attribute.RenamerId, Name = p.Name, Settings = settings
        };
    }

    /// <summary>
    /// Get the Renamer by the given Config Name
    /// </summary>
    /// <param name="renamerName">Config Name</param>
    /// <returns></returns>
    [HttpGet("Config/{renamerName}/Renamer")]
    public ActionResult<ApiRenamer> GetRenamerFromConfig([FromRoute] string renamerName)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(renamerName);
        if (renamerConfig == null)
            return NotFound("Config not found");
        if (!_renameFileService.RenamersByType.TryGetValue(renamerConfig.Type, out var value))
            return NotFound("Renamer not found");

        return GetRenamer(value, true);
    }

    /// <summary>
    /// Get the Config by the given Name
    /// </summary>
    /// <param name="renamerName">Config Name</param>
    /// <returns></returns>
    [HttpGet("Config/{renamerName}")]
    public ActionResult<RenamerConfig> GetRenamerConfig([FromRoute] string renamerName)
    {
        var config = _renamerConfigRepository.GetByName(renamerName);
        if (config == null)
            return NotFound("Config not found");

        return GetRenamerConfig(config);
    }

    /// <summary>
    /// Create a new Config
    /// </summary>
    /// <param name="body">Config</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Config")]
    public ActionResult<RenamerConfig> PostRenamerConfig([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RenamerConfig body)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Name is required");
        if (!_renameFileService.RenamersByKey.TryGetValue(body.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        var existingRenamer = _renamerConfigRepository.GetByName(body.Name);
        if (existingRenamer != null)
            return Conflict($"Config with name {body.Name} already exists");

        var config = new Shoko.Server.Models.RenamerConfig
        {
            Name = body.Name,
            Type = renamer.GetType(),
        };

        if (!ApplyRenamerConfigSettings(body, config))
            return ValidationProblem(ModelState);

        _renamerConfigRepository.Save(config);

        return GetRenamerConfig(config);
    }

    /// <summary>
    /// Update the Config by the given Name
    /// </summary>
    /// <param name="renamerName">Config Name</param>
    /// <param name="body">Config</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Config/{renamerName}")]
    public ActionResult<RenamerConfig> PutRenamerConfig([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RenamerConfig body)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(renamerName);
        if (renamerConfig == null)
            return NotFound("Config not found");

        if (!_renameFileService.RenamersByKey.TryGetValue(body.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        var temp = renamerConfig.DeepClone();

        temp.Type = renamer.GetType();
        if (!ApplyRenamerConfigSettings(body, temp))
            return ValidationProblem(ModelState);

        temp.DeepCloneTo(renamerConfig);
        _renamerConfigRepository.Save(renamerConfig);

        return GetRenamerConfig(renamerConfig);
    }

    private bool ApplyRenamerConfigSettings(RenamerConfig body, Shoko.Server.Models.RenamerConfig renamerConfig)
    {
        if (body.Settings == null) return true;
        var result = true;
        var settingsType = renamerConfig.Type.GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        if (settingsType == null) return true;

        renamerConfig.Settings ??= ActivatorUtilities.CreateInstance(Utils.ServiceContainer, settingsType);

        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var setting in body.Settings)
        {
            var property = properties.FirstOrDefault(x => x.Name == setting.Name) ?? properties.FirstOrDefault(x => x.GetCustomAttribute<RenamerSettingAttribute>()?.Name == setting.Name);

            if (property == null)
                continue;

            if (setting.Value?.GetType() != property.PropertyType)
            {
                ModelState.AddModelError("Settings[" + setting.Name + "].Value", "Value must be of type " + property.PropertyType.Name);
                result = false;
                continue;
            }
            property.SetValue(renamerConfig.Settings, Convert.ChangeType(setting.Value, property.PropertyType));
        }

        return result;
    }

    /// <summary>
    /// Applies a JSON patch document to modify the Config with the given Name
    /// </summary>
    /// <param name="renamerName">
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
    [HttpPatch("Config/{renamerName}")]
    public ActionResult<RenamerConfig> PatchRenamer([FromRoute] string renamerName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<RenamerConfig> patchDocument)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(renamerName);
        if (renamerConfig == null)
            return NotFound("Config not found.");

        // Patch the renamer in the v3 model and merge it back into the
        // settings.
        var modifyRenamer = GetRenamerConfig(renamerConfig);
        patchDocument.ApplyTo(modifyRenamer, ModelState);

        // validate
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var existingRenamer = _renamerConfigRepository.GetByName(modifyRenamer.Name);
        if (existingRenamer != null && existingRenamer.ID != renamerConfig.ID)
            return Conflict($"Renamer with name {modifyRenamer.Name} already exists.");

        if (!_renameFileService.RenamersByKey.TryGetValue(modifyRenamer.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        // apply
        var temp = renamerConfig.DeepClone();
        temp.Name = modifyRenamer.Name;
        temp.Type = renamer.GetType();

        if (!ApplyRenamerConfigSettings(modifyRenamer, temp))
            return ValidationProblem(ModelState);

        temp.DeepCloneTo(renamerConfig);

        _renamerConfigRepository.Save(renamerConfig);

        return GetRenamerConfig(renamerConfig);
    }

    /// <summary>
    /// Update the Config by the given Name
    /// </summary>
    /// <param name="renamerName">Config Name</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Config/{renamerName}")]
    public ActionResult DeleteRenamerConfig([FromRoute] string renamerName)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(renamerName);
        if (renamerConfig == null)
            return NotFound("Config not found");

        _renamerConfigRepository.Delete(renamerConfig);

        return Ok();
    }

    /// <summary>
    /// Execute the script and either preview the changes or commit the changes
    /// on a batch of files.
    /// </summary>
    /// <param name="args">A model for the arguments</param>
    /// <param name="move">Whether or not to get the destination of the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to get the new name of the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Preview")]
    public ActionResult<IEnumerable<ApiRenamer.RelocateResult>> BatchRelocateFilesByScriptID([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchRelocateArgs args, bool? move = null, bool? rename = null)
    {
        Shoko.Server.Models.RenamerConfig? config = null;
        if (args.Config != null)
        {
            config = new Shoko.Server.Models.RenamerConfig { Name = args.Config.Name };
            if (!_renameFileService.RenamersByKey.TryGetValue(args.Config.RenamerID, out var renamer))
                return NotFound("Renamer not found");

            config.Type = renamer.GetType();

            if (!ApplyRenamerConfigSettings(args.Config, config))
                return ValidationProblem(ModelState);
        }

        var settings = _settingsProvider.GetSettings();
        var configName = settings.Plugins.Renamer.DefaultRenamer;
        config ??= _renamerConfigRepository.GetByName(configName);
        if (config is null)
            return NotFound("Default Config not found");

        if (!_renameFileService.RenamersByType.ContainsKey(config.Type))
            return BadRequest("Renamer for Default Config not found");

        var results = GetNewLocationsForFiles(args.FileIDs, config, move ?? settings.Plugins.Renamer.MoveOnImport,
            rename ?? settings.Plugins.Renamer.RenameOnImport);
        return Ok(results);
    }

    /// <summary>
    /// Execute the script and either preview the changes or commit the changes
    /// on a batch of files.
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <param name="fileIDs">The file IDs to preview</param>
    /// <param name="move">Whether or not to get the destination of the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to get the new name of the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Config/{configName}/Preview")]
    public ActionResult<IEnumerable<ApiRenamer.RelocateResult>> BatchRelocateFilesByScriptID([FromRoute] string configName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs, bool? move = null, bool? rename = null)
    {
        var config = _renamerConfigRepository.GetByName(configName);
        if (config is null)
            return NotFound("Config not found");

        if (!_renameFileService.RenamersByType.ContainsKey(config.Type))
            return BadRequest("Renamer for Config not found");

        var settings = _settingsProvider.GetSettings();
        var results = GetNewLocationsForFiles(fileIDs, config, move ?? settings.Plugins.Renamer.MoveOnImport,
            rename ?? settings.Plugins.Renamer.RenameOnImport);
        return Ok(results);
    }

    [NonAction]
    private IEnumerable<ApiRenamer.RelocateResult> GetNewLocationsForFiles(IEnumerable<int> fileIDs, Shoko.Server.Models.RenamerConfig config, bool move, bool rename)
    {
        foreach (var vlID in fileIDs)
        {
            var vl = vlID > 0 ? _vlRepository.GetByID(vlID) : null;
            if (vl is null)
            {
                yield return new ApiRenamer.RelocateResult
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
                yield return new ApiRenamer.RelocateResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    ErrorMessage = vlp is not null
                        ? $"Unable to find any resolvable File.Location for File with ID {vlID}. Found valid but non-resolvable File.Location \"{vlp.FullServerPath}\" with ID {vlp.VideoLocal_Place_ID}."
                        : $"Unable to find any resolvable File.Location for File with ID {vlID}.",
                };
                continue;
            }

            var result = _renameFileService.GetNewPath(vlp, config, move, rename);
            yield return new ApiRenamer.RelocateResult
            {
                FileID = vlID,
                IsSuccess = result.Error is null,
                ErrorMessage = result.Error?.Message,
                FileLocationID = vlp.VideoLocal_Place_ID
            };
        }
    }

    /*
    /// <summary>
    /// Execute the script and either preview the changes or commit the changes
    /// on a batch of files.
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <param name="body">Contains the files, renamer and script to use for the preview.</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Config/{configName}/Execute")]
    public ActionResult<IAsyncEnumerable<ApiRenamer.RelocateResult>> BatchRelocateFilesByScriptID([FromRoute] string configName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ApiRenamer.Input.BatchAutoRelocateBody body)
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
