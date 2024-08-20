using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Force.DeepCloner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko.Relocation;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using ApiRenamer = Shoko.Server.API.v3.Models.Shoko.Relocation.Renamer;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;
using RelocationResult = Shoko.Server.API.v3.Models.Shoko.Relocation.RelocationResult;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class RenamerController : BaseController
{
    private readonly ILogger<RenamerController> _logger;
    private readonly ImportFolderRepository _importFolderRepository;
    private readonly VideoLocalRepository _vlRepository;
    private readonly VideoLocal_PlaceRepository _vlpRepository;
    private readonly VideoLocal_PlaceService _vlpService;
    private readonly RenamerConfigRepository _renamerConfigRepository;
    private readonly RenameFileService _renameFileService;
    private readonly ISettingsProvider _settingsProvider;

    public RenamerController(ILogger<RenamerController> logger, ISettingsProvider settingsProvider, VideoLocal_PlaceService vlpService, VideoLocalRepository vlRepository, RenamerConfigRepository renamerConfigRepository, RenameFileService renameFileService, ImportFolderRepository importFolderRepository, VideoLocal_PlaceRepository vlpRepository) : base(settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _vlpService = vlpService;
        _vlRepository = vlRepository;
        _renamerConfigRepository = renamerConfigRepository;
        _renameFileService = renameFileService;
        _importFolderRepository = importFolderRepository;
        _vlpRepository = vlpRepository;
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
        var settings = new List<SettingDefinition>();
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
            var rangeAttribute = property.GetCustomAttribute<RangeAttribute>();
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

            settings.Add(new SettingDefinition
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Description = renamerSettingAttribute?.Description,
                Language = settingType is RenamerSettingType.Code ? renamerSettingAttribute?.Language : null,
                SettingType = settingType,
                MinimumValue = rangeAttribute?.Minimum,
                MaximumValue = rangeAttribute?.Maximum,
            });
        }

        var defaultSettings = new List<Setting>();
        properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var defaultSettingsObject = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(a => a.Name == "DefaultSettings")?.GetMethod?.Invoke(renamer, null);

        foreach (var property in properties)
        {
            var renamerSettingAttribute = property.GetCustomAttribute<RenamerSettingAttribute>();
            defaultSettings.Add(new Setting
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Value = property.GetValue(defaultSettingsObject)
            });
        }
        return new ApiRenamer
        {
            RenamerID = attribute.RenamerId,
            Name = renamer.Name,
            Description = renamer.Description,
            Version = renamer.GetType().Assembly.GetName().Version?.ToString(),
            Enabled = enabled,
            Settings = settings,
            DefaultSettings = defaultSettings
        };
    }

    /// <summary>
    /// Get a list of all Configs
    /// </summary>
    /// <returns></returns>
    [HttpGet("Config")]
    public ActionResult<List<RenamerConfig>> GetAllRenamerConfigs()
    {
        return _renamerConfigRepository.GetAll().Select(GetRenamerConfig).WhereNotNull().ToList();
    }

    private static RenamerConfig GetRenamerConfig(Shoko.Server.Models.RenamerConfig p)
    {
        // p.Type can be null if the config exists but the renamer doesn't
        if (p.Type is null)
            return new RenamerConfig { RenamerID = string.Empty, Name = p.Name };

        // we can suppress nullability, because we check this when loading
        var attribute = p.Type.GetCustomAttributes<RenamerIDAttribute>().FirstOrDefault()!;
        var settingsType = p.Type.GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetGenericArguments().FirstOrDefault();
        var settings = new List<Setting>();
        if (settingsType == null)
            return new RenamerConfig { RenamerID = attribute.RenamerId, Name = p.Name, Settings = settings };

        // settings
        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var renamerSettingAttribute = property.GetCustomAttribute<RenamerSettingAttribute>();
            settings.Add(new Setting
            {
                Name = renamerSettingAttribute?.Name ?? property.Name,
                Value = property.GetValue(p.Settings)
            });
        }

        return new RenamerConfig
        {
            RenamerID = attribute.RenamerId,
            Name = p.Name,
            Settings = settings,
        };
    }

    /// <summary>
    /// Get the Renamer by the given Config Name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <returns></returns>
    [HttpGet("Config/{configName}/Renamer")]
    public ActionResult<ApiRenamer> GetRenamerFromConfig([FromRoute] string configName)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(configName);
        if (renamerConfig == null)
            return NotFound("Config not found");
        if (!_renameFileService.RenamersByType.TryGetValue(renamerConfig.Type, out var value))
            return NotFound("Renamer not found");

        return GetRenamer(value, true);
    }

    /// <summary>
    /// Get the Config by the given Name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <returns></returns>
    [HttpGet("Config/{configName}")]
    public ActionResult<RenamerConfig> GetRenamerConfig([FromRoute] string configName)
    {
        var config = _renamerConfigRepository.GetByName(configName);
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

        if (body.Settings == null || body.Settings.Count == 0)
        {
            var defaultSettingsObject = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
                ?.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(a => a.Name == "DefaultSettings")?.GetMethod?.Invoke(renamer, null);
            config.Settings = defaultSettingsObject;
        }

        _renamerConfigRepository.Save(config);

        return GetRenamerConfig(config);
    }

    /// <summary>
    /// Update the Config by the given Name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <param name="body">Config</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("Config/{configName}")]
    public ActionResult<RenamerConfig> PutRenamerConfig([FromRoute] string configName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RenamerConfig body)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(configName);
        if (renamerConfig == null)
            return NotFound("Config not found");

        if (!_renameFileService.RenamersByKey.TryGetValue(body.RenamerID, out var renamer))
            return NotFound("Renamer not found");

        var oldName = renamerConfig.Name;
        var temp = renamerConfig.DeepClone();

        temp.Type = renamer.GetType();
        if (body.Settings == null || body.Settings.Count == 0)
            return BadRequest("Settings are required for a put request");

        if (!ApplyRenamerConfigSettings(body, temp))
            return ValidationProblem(ModelState);

        temp.DeepCloneTo(renamerConfig);
        _renamerConfigRepository.Save(renamerConfig);

        // update default renamer in settings.
        var settings = _settingsProvider.GetSettings();
        var nameChanged = renamerConfig.Name != oldName;
        if (nameChanged && settings.Plugins.Renamer.DefaultRenamer == oldName)
        {
            settings.Plugins.Renamer.DefaultRenamer = renamerConfig.Name;
            _settingsProvider.SaveSettings(settings);
        }

        return GetRenamerConfig(renamerConfig);
    }

    private bool ApplyRenamerConfigSettings(RenamerConfig body, Shoko.Server.Models.RenamerConfig renamerConfig)
    {
        if (body.Settings == null || body.Settings.Count == 0) return true;
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

            try
            {
                var convertedValue = Convert.ChangeType(setting.Value, property.PropertyType);
                property.SetValue(renamerConfig.Settings, convertedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Setting {Setting} has an invalid type {ActualPropertyType}, but should be of type {PropertyType}", setting.Name, setting.Value?.GetType().Name, property.PropertyType.Name);
                ModelState.AddModelError("Settings[" + setting.Name + "].Value", "Value must be of type " + property.PropertyType.Name);
                result = false;
                continue;
            }
        }

        return result;
    }

    /// <summary>
    /// Applies a JSON patch document to modify the Config with the given Name
    /// </summary>
    /// <param name="configName">
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
    [HttpPatch("Config/{configName}")]
    public ActionResult<RenamerConfig> PatchRenamer([FromRoute] string configName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<RenamerConfig> patchDocument)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(configName);
        if (renamerConfig == null)
            return NotFound("Config not found.");

        // Patch the renamer in the v3 model and merge it back into the
        // settings.
        var oldName = renamerConfig.Name;
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

        // update default renamer in settings.
        var settings = _settingsProvider.GetSettings();
        var nameChanged = modifyRenamer.Name != oldName;
        if (nameChanged && settings.Plugins.Renamer.DefaultRenamer == oldName)
        {
            settings.Plugins.Renamer.DefaultRenamer = modifyRenamer.Name;
            _settingsProvider.SaveSettings(settings);
        }

        return GetRenamerConfig(renamerConfig);
    }

    /// <summary>
    /// Delete the Config by the given Name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Config/{configName}")]
    public ActionResult DeleteRenamerConfig([FromRoute] string configName)
    {
        var renamerConfig = _renamerConfigRepository.GetByName(configName);
        if (renamerConfig == null)
            return NotFound("Config not found");

        var settings = _settingsProvider.GetSettings();
        if (settings.Plugins.Renamer.DefaultRenamer == configName)
            return ValidationProblem("Default renamer config cannot be deleted!");

        _renamerConfigRepository.Delete(renamerConfig);

        return Ok();
    }

    /// <summary>
    /// Preview the changes made by a provided Config
    /// </summary>
    /// <param name="args">A model for the arguments</param>
    /// <param name="move">Whether or not to get the destination of the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to get the new name of the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Preview")]
    public ActionResult<IEnumerable<RelocationResult>> BatchPreviewFilesByScriptID([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] BatchRelocateBody args, bool? move = null, bool? rename = null)
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
    /// Preview the changes made by an existing Config, by the given Config Name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <param name="fileIDs">The file IDs to preview</param>
    /// <param name="move">Whether or not to get the destination of the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to get the new name of the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocate results.</returns>
    [Authorize("admin")]
    [HttpPost("Config/{configName}/Preview")]
    public ActionResult<IEnumerable<RelocationResult>> BatchRelocateFilesByScriptID([FromRoute] string configName, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs, bool? move = null, bool? rename = null)
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

    private IEnumerable<RelocationResult> GetNewLocationsForFiles(IEnumerable<int> fileIDs, Shoko.Server.Models.RenamerConfig config, bool move, bool rename)
    {
        foreach (var vlID in fileIDs)
        {
            var vl = vlID > 0 ? _vlRepository.GetByID(vlID) : null;
            if (vl is null)
            {
                yield return new RelocationResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    IsPreview = true,
                    ErrorMessage = $"Unable to find File with ID {vlID}",
                };
                continue;
            }

            var vlp = vl.FirstResolvedPlace;
            if (vlp is null)
            {
                vlp = vl.FirstValidPlace;
                yield return new RelocationResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    IsPreview = true,
                    ErrorMessage = vlp is not null
                        ? $"Unable to find any resolvable File.Location for File with ID {vlID}. Found valid but non-resolvable File.Location \"{vlp.FullServerPath}\" with ID {vlp.VideoLocal_Place_ID}."
                        : $"Unable to find any resolvable File.Location for File with ID {vlID}.",
                };
                continue;
            }

            var result = _renameFileService.GetNewPath(vlp, config, move, rename);

            yield return new RelocationResult
            {
                FileID = vlID,
                IsSuccess = result.Success,
                IsPreview = true,
                IsRelocated = result.Moved || result.Renamed,
                ConfigName = config.ID > 0 ? config.Name : null,
                AbsolutePath = result.AbsolutePath,
                ImportFolderID = result.ImportFolder?.ID,
                RelativePath = result.RelativePath,
                ErrorMessage = result.ErrorMessage,
                FileLocationID = vlp.VideoLocal_Place_ID
            };
        }
    }

    /// <summary>
    /// Directly relocates a file to a new location specified by the user.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be relocated.</param>
    /// <param name="body">New location information.</param>
    /// <returns>A result object containing information about the relocation process.</returns>
    [Authorize("admin")]
    [HttpPost("Relocate/Location/{locationID}")]
    public async Task<ActionResult<RelocationResult>> DirectlyRelocateFileLocation([FromRoute, Range(1, int.MaxValue)] int locationID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RelocateBody body)
    {
        var fileLocation = _vlpRepository.GetByID(locationID);
        if (fileLocation == null)
            return NotFound(FileController.FileLocationNotFoundWithLocationID);

        var importFolder = _importFolderRepository.GetByID(body.ImportFolderID);
        if (importFolder == null)
            return BadRequest($"Unknown import folder with the given id `{body.ImportFolderID}`.");

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(importFolder.ImportFolderLocation, body.RelativePath));
        if (!fullPath.StartsWith(importFolder.ImportFolderLocation, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The provided relative path leads outside the import folder.");
        var sanitizedRelativePath = Path.GetRelativePath(importFolder.ImportFolderLocation, fullPath);

        // Store the old import folder id and relative path for comparison.
        var oldImportFolderId = fileLocation.ImportFolderID;
        var oldRelativePath = fileLocation.FilePath;

        // Rename and move the file.
        var result = await _vlpService.DirectlyRelocateFile(
            fileLocation,
            new DirectRelocateRequest
            {
                ImportFolder = importFolder,
                RelativePath = sanitizedRelativePath,
                DeleteEmptyDirectories = body.DeleteEmptyDirectories
            }
        );
        if (!result.Success)
            return new RelocationResult
            {
                FileID = fileLocation.VideoLocalID,
                FileLocationID = fileLocation.VideoLocal_Place_ID,
                IsSuccess = false,
                ErrorMessage = result.ErrorMessage,
            };

        // Check if it was actually relocated, or if we landed on the same location as earlier.
        var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldImportFolderId != result.ImportFolder.ID;
        return new RelocationResult
        {
            FileID = fileLocation.VideoLocalID,
            FileLocationID = fileLocation.VideoLocal_Place_ID,
            ImportFolderID = result.ImportFolder.ID,
            IsSuccess = true,
            IsRelocated = relocated,
            RelativePath = result.RelativePath,
            AbsolutePath = result.AbsolutePath,
        };
    }

    /// <summary>
    /// Relocate a batch of files using a Config of the given name
    /// </summary>
    /// <param name="configName">Config Name</param>
    /// <param name="fileIDs">The files to relocate</param>
    /// <param name="deleteEmptyDirectories">Whether or not to delete empty directories</param>
    /// <param name="move">Whether or not to move the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to rename the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocation results.</returns>
    [Authorize("admin")]
    [HttpPost("Config/{configName}/Relocate")]
    public ActionResult<IAsyncEnumerable<RelocationResult>> BatchRelocateFilesByConfig([FromRoute] string configName,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs, [FromQuery] bool deleteEmptyDirectories = true,
        [FromQuery] bool? move = null, [FromQuery] bool? rename = null)
    {
        var config = _renamerConfigRepository.GetByName(configName);
        if (config is null)
            return NotFound("Config not found.");

        if (!_renameFileService.RenamersByType.ContainsKey(config.Type))
            return BadRequest("Renamer not found.");

        var settings = _settingsProvider.GetSettings();
        return new ActionResult<IAsyncEnumerable<RelocationResult>>(
            InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
            {
                Renamer = config,
                DeleteEmptyDirectories = deleteEmptyDirectories,
                Move = move ?? settings.Plugins.Renamer.MoveOnImport,
                Rename = rename ?? settings.Plugins.Renamer.RenameOnImport
            })
        );
    }

    /// <summary>
    /// Relocate a batch of files using the default Config
    /// </summary>
    /// <param name="fileIDs">The files to relocate</param>
    /// <param name="deleteEmptyDirectories">Whether or not to delete empty directories</param>
    /// <param name="move">Whether or not to move the files. If `null`, defaults to `Settings.Import.MoveOnImport`</param>
    /// <param name="rename">Whether or not to rename the files. If `null`, defaults to `Settings.Import.RenameOnImport`</param>
    /// <returns>A stream of relocation results.</returns>
    [Authorize("admin")]
    [HttpPost("Relocate")]
    public ActionResult<IAsyncEnumerable<RelocationResult>> BatchRelocateFilesWithDefaultConfig([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] IEnumerable<int> fileIDs, [FromQuery] bool deleteEmptyDirectories = true, [FromQuery] bool? move = null, [FromQuery] bool? rename = null)
    {
        var settings = _settingsProvider.GetSettings();
        var configName = settings.Plugins.Renamer.DefaultRenamer;
        if (string.IsNullOrEmpty(configName)) return BadRequest("Default Config not set. Set it in Settings > Plugins > Renamer > DefaultRenamer");

        var config = _renamerConfigRepository.GetByName(configName);
        if (config is null)
            return NotFound("Config not found.");

        if (!_renameFileService.RenamersByType.ContainsKey(config.Type))
            return BadRequest("Renamer not found.");

        return new ActionResult<IAsyncEnumerable<RelocationResult>>(
            InternalBatchRelocateFiles(fileIDs, new AutoRelocateRequest
            {
                Renamer = config,
                DeleteEmptyDirectories = deleteEmptyDirectories,
                Move = move ?? settings.Plugins.Renamer.MoveOnImport,
                Rename = rename ?? settings.Plugins.Renamer.RenameOnImport
            })
        );
    }

    private async IAsyncEnumerable<RelocationResult> InternalBatchRelocateFiles(IEnumerable<int> fileIDs, AutoRelocateRequest request)
    {
        var defaultConfig = _settingsProvider.GetSettings().Plugins.Renamer.DefaultRenamer;
        var configName = request.Renamer?.Name ?? _renamerConfigRepository.GetByName(defaultConfig)?.Name;
        foreach (var vlID in fileIDs)
        {
            var vl = vlID > 0 ? _vlRepository.GetByID(vlID) : null;
            if (vl is null)
            {
                yield return new RelocationResult
                {
                    FileID = vlID,
                    IsSuccess = false,
                    ConfigName = configName,
                    ErrorMessage = $"Unable to find File with ID {vlID}",
                };
                continue;
            }

            var vlp = vl.FirstResolvedPlace;
            if (vlp is null)
            {
                vlp = vl.FirstValidPlace;
                yield return new RelocationResult
                {
                    FileID = vlID,
                    ConfigName = configName,
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
                yield return new RelocationResult
                {
                    FileID = vlp.VideoLocalID,
                    FileLocationID = vlp.VideoLocal_Place_ID,
                    ConfigName = configName,
                    IsSuccess = false,
                    ErrorMessage = result.ErrorMessage,
                };
                continue;
            }

            Renamer.RelocationResult? otherResult = null;
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
                yield return new RelocationResult
                {
                    FileID = vlp.VideoLocalID,
                    FileLocationID = vlp.VideoLocal_Place_ID,
                    ConfigName = configName,
                    IsSuccess = false,
                    ErrorMessage = result.ErrorMessage,
                };
                continue;
            }

            // Check if it was actually relocated, or if we landed on the same location as earlier.
            var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldImportFolderId != result.ImportFolder.ID;
            yield return new RelocationResult
            {
                FileID = vlp.VideoLocalID,
                FileLocationID = vlp.VideoLocal_Place_ID,
                ImportFolderID = result.ImportFolder.ID,
                ConfigName = configName,
                IsSuccess = true,
                IsRelocated = relocated,
                RelativePath = result.RelativePath,
                AbsolutePath = result.AbsolutePath
            };
        }
    }
}
