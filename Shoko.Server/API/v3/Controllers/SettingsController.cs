using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]"), Tags("Configuration")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class SettingsController(
    ISettingsProvider settingsProvider,
    ConfigurationProvider<ServerSettings> configurationProvider,
    IConfigurationService configurationService,
    IOptions<MvcNewtonsoftJsonOptions> jsonOptions,
    ILogger<SettingsController> logger,
    AniDBUDPConnectionHandler udpHandler,
    IUserConfigurationService userConfigurationService
) : BaseController(settingsProvider)
{
    private readonly ConfigurationProvider<ServerSettings> _configurationProvider = configurationProvider;

    private readonly IConfigurationService _configurationService = configurationService;

    private readonly IOptions<MvcNewtonsoftJsonOptions> _jsonOptions = jsonOptions;

    private readonly AniDBUDPConnectionHandler _udpHandler = udpHandler;

    private readonly ILogger<SettingsController> _logger = logger;

    private readonly IUserConfigurationService _userConfigurationService = userConfigurationService;

    /// <summary>
    /// The name the web UI's settings had in the server settings, under which
    /// the settings route still reads and writes the caller's own <c>webui</c>
    /// configuration.
    /// </summary>
    /// <remarks>
    /// A temporary bridge: it goes once the WebUI reads and writes
    /// <c>/api/v3/User/Current/Configuration/webui</c> itself.
    /// </remarks>
    internal const string WebUISettingsName = "WebUI_Settings";

    // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
    // Some may be patched to "", though.

    // TODO some way of distinguishing what a normal user vs an admin can set.

    /// <summary>
    /// Get all settings
    /// </summary>
    /// <remarks>
    /// The settings are serialized here rather than handed to the formatter as
    /// an object, because every secret is masked on the way out and masking
    /// works on the serialized document. The serializer is the one MVC would
    /// have used, so the shape of the response is unchanged. <c>WebUI_Settings</c>
    /// is the caller's own <c>webui</c> configuration, stringified, for web UIs that
    /// still read them here.
    /// </remarks>
    /// <returns></returns>
    [Produces("application/json")]
    [ProducesResponseType<IServerSettings>(StatusCodes.Status200OK)]
    [HttpGet]
    public ActionResult GetSettings()
    {
        var settings = (ServerSettings)SettingsProvider.GetSettings();
        var serializer = JsonSerializer.Create(_jsonOptions.Value.SerializerSettings);
        var document = JObject.FromObject(settings, serializer);
        var webUISettings = IsSetupUser ? null : _userConfigurationService.Load(User, UserConfigurationService.WebUIConfigurationID);
        document[WebUISettingsName] = webUISettings ?? string.Empty;
        var json = document.ToString(Formatting.None);
        return Content(_configurationService.MaskSecrets(_configurationProvider.ConfigurationInfo, json), "application/json");
    }

    /// <summary>
    /// JsonPatch the settings
    /// </summary>
    /// <param name="settings">JsonPatch operations</param>
    /// <returns></returns>
    [HttpPatch]
    public ActionResult SetSettings([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ServerSettings> settings)
    {
        try
        {
            var existingSettings = (ServerSettings)SettingsProvider.GetSettings(copy: true);
            if (!TryTakeWebUISettings(settings, ModelState, out var webUIChanged, out var webUISettings))
                return ValidationProblem(ModelState);

            ApplyPatch(settings, existingSettings, ModelState);
            if (!ModelState.IsValid)
            {
                _logger.LogDebug("Failed to apply settings patch: {ModelState}", JsonConvert.SerializeObject(ModelState));
                return ValidationProblem(ModelState);
            }

            SettingsProvider.SaveSettings(existingSettings);
            if (webUIChanged && !IsSetupUser)
            {
                if (webUISettings is null)
                    _userConfigurationService.Delete(User, UserConfigurationService.WebUIConfigurationID);
                else
                    _userConfigurationService.Save(User, UserConfigurationService.WebUIConfigurationID, webUISettings.ToString(Formatting.None));
            }

            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Whether the caller is the first-run setup's stand-in user, who has no
    /// settings of their own and may be calling before the database exists.
    /// </summary>
    private bool IsSetupUser
        => User.JMMUserID is 0;

    /// <summary>
    /// Takes the operations on <c>WebUI_Settings</c> out of a settings patch,
    /// as the server settings no longer hold them, and works out what the
    /// caller's <c>webui</c> configuration becomes.
    /// </summary>
    /// <param name="patch">The patch, left without those operations.</param>
    /// <param name="modelState">Where to add an error.</param>
    /// <param name="changed">Whether the patch touches them.</param>
    /// <param name="webUISettings">The new configuration, or <c>null</c> to remove it.</param>
    /// <returns><c>false</c> when the patch sets them to something other than a JSON object.</returns>
    internal static bool TryTakeWebUISettings(
        JsonPatchDocument<ServerSettings> patch,
        ModelStateDictionary modelState,
        out bool changed,
        out JObject? webUISettings
    )
    {
        changed = false;
        webUISettings = null;
        var operations = patch.Operations
            .Where(operation => string.Equals(operation.path?.Trim('/'), WebUISettingsName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (operations.Count is 0)
            return true;

        foreach (var operation in operations)
            patch.Operations.Remove(operation);

        changed = true;
        var last = operations[^1];
        var text = last.OperationType is OperationType.Remove ? null : last.value is JToken token ? token.Value<string>() : last.value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        try
        {
            webUISettings = JObject.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            modelState.AddModelError(WebUISettingsName, "The web UI settings must be a JSON object.");
            return false;
        }
    }

    /// <summary>
    /// Applies a patch to the server settings, reading metadata sources as the
    /// settings file does, so a source whose plugin isn't loaded can be sent
    /// back as <see cref="GetSettings"/> sent it.
    /// </summary>
    /// <param name="patch">The patch.</param>
    /// <param name="settings">The settings to patch.</param>
    /// <param name="modelState">Where to add any error.</param>
    internal static void ApplyPatch(JsonPatchDocument<ServerSettings> patch, ServerSettings settings, ModelStateDictionary modelState)
    {
        patch.ContractResolver = ApiContractResolver.ForSettings;
        patch.ApplyTo(settings, modelState);
    }

    /// <summary>
    /// Tests a Login with the given Credentials. This does not save the credentials.
    /// </summary>
    /// <param name="credentials">POST the body as a <see cref="Credentials"/> object</param>
    /// <returns></returns>
    [HttpPost("AniDB/TestLogin")]
    public async Task<ActionResult> TestAniDB([FromBody] Credentials credentials)
    {
        var cancellationToken = HttpContext.RequestAborted;
        _logger.LogInformation("Testing AniDB Login and Connection");
        if (string.IsNullOrWhiteSpace(credentials.Username))
            ModelState.AddModelError(nameof(credentials.Username), "Username cannot be empty.");

        if (string.IsNullOrWhiteSpace(credentials.Password))
            ModelState.AddModelError(nameof(credentials.Password), "Password cannot be empty.");

        if (!ModelState.IsValid)
        {
            _logger.LogInformation("Failed AniDB Login and Connection: {ModelState}", JsonConvert.SerializeObject(ModelState));
            return ValidationProblem(ModelState);
        }

        var settings = SettingsProvider.GetSettings();
        // The settings endpoint hands out a masked password, so a client that
        // posts what it was given, rather than what the user retyped, sends the
        // mask. It stands for the stored password, which is what testing
        // without retyping means.
        var password = ConfigurationSecrets.Resolve(credentials.Password, settings.AniDb.Password);
        if (string.IsNullOrWhiteSpace(password))
            return ValidationProblem("There is no stored password to test with.", nameof(credentials.Password));

        if (!_udpHandler.IsAlive)
            await _udpHandler.InitAsync(credentials.Username, password, settings.AniDb.UDPServerAddress, settings.AniDb.UDPServerPort, settings.AniDb.ClientPort, cancellationToken);
        else await _udpHandler.ForceLogoutAsync(cancellationToken);

        if (!await _udpHandler.TestLoginAsync(credentials.Username, password, cancellationToken))
        {
            _logger.LogInformation("Failed AniDB Login and Connection");
            return ValidationProblem("Failed to log in.", "Connection");
        }

        return Ok();
    }

    /// <summary>
    /// Get a list of all supported languages.
    /// </summary>
    /// <returns>A list of all supported languages.</returns>
    [HttpGet("SupportedLanguages")]
    public ActionResult<List<LanguageDetails>> GetAllSupportedLanguages() =>
        Languages.AllNamingLanguages.Select(a => new LanguageDetails(a.Language)).ToList();
}
