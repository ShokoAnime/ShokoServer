using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class SettingsController : BaseController
{
    private readonly IConnectivityService _connectivityService;
    private readonly IUDPConnectionHandler _udpHandler;
    private readonly IHttpConnectionHandler _httpHandler;
    private readonly ILogger<SettingsController> _logger;

    // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
    // Some may be patched to "", though.

    // TODO some way of distinguishing what a normal user vs an admin can set.

    /// <summary>
    /// Get all settings
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IServerSettings> GetSettings()
    {
        return new ActionResult<IServerSettings>(SettingsProvider.GetSettings());
    }

    /// <summary>
    /// JsonPatch the settings
    /// </summary>
    /// <param name="settings">JsonPatch operations</param>
    /// <param name="skipValidation">Skip Model Validation. Use with caution</param>
    /// <returns></returns>
    [HttpPatch]
    public ActionResult SetSettings([FromBody] JsonPatchDocument<ServerSettings> settings, bool skipValidation = false)
    {
        if (settings == null)
        {
            return ValidationProblem("The settings object is invalid.");
        }

        var existingSettings = SettingsProvider.GetSettings(copy: true);
        settings.ApplyTo((ServerSettings)existingSettings, ModelState);
        if (!skipValidation && !TryValidateModel(existingSettings))
            return ValidationProblem(ModelState);

        SettingsProvider.SaveSettings(existingSettings);
        return Ok();
    }

    /// <summary>
    /// Tests a Login with the given Credentials. This does not save the credentials.
    /// </summary>
    /// <param name="credentials">POST the body as a <see cref="Credentials"/> object</param>
    /// <returns></returns>
    [HttpPost("AniDB/TestLogin")]
    public async Task<ActionResult> TestAniDB([FromBody] Credentials credentials)
    {
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

        var alive = _udpHandler.IsAlive;
        var settings = SettingsProvider.GetSettings();
        if (!_udpHandler.IsAlive)
            await _udpHandler.Init(credentials.Username, credentials.Password, settings.AniDb.UDPServerAddress, settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);
        else _udpHandler.ForceLogout();

        if (!await _udpHandler.TestLogin(credentials.Username, credentials.Password))
        {
            _logger.LogInformation("Failed AniDB Login and Connection");
            return ValidationProblem("Failed to log in.", "Connection");
        }

        if (!alive) await _udpHandler.CloseConnections();
        return Ok();
    }

    /// <summary>
    /// Gets the current network connectivity details for the server.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Connectivity")]
    public ActionResult<ConnectivityDetails> GetNetworkAvailability()
    {
        return new ConnectivityDetails
        {
            NetworkAvailability = _connectivityService.NetworkAvailability,
            LastChangedAt = _connectivityService.LastChangedAt,
            IsAniDBUdpReachable = _udpHandler.IsAlive && _udpHandler.IsNetworkAvailable,
            IsAniDBUdpBanned = _udpHandler.IsBanned,
            IsAniDBHttpBanned = _httpHandler.IsBanned
        };
    }

    /// <summary>
    /// Forcefully re-checks the current network connectivity, then returns the
    /// updated details for the server.
    /// </summary>
    /// <returns></returns>
    [HttpPost("Connectivity")]
    public async Task<ActionResult<object>> CheckNetworkAvailability()
    {
        await _connectivityService.CheckAvailability();

        return GetNetworkAvailability();
    }

    public SettingsController(ISettingsProvider settingsProvider, IConnectivityService connectivityService, ILogger<SettingsController> logger, IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler) : base(settingsProvider)
    {
        _connectivityService = connectivityService;
        _logger = logger;
        _udpHandler = udpHandler;
        _httpHandler = httpHandler;
    }

    /// <summary>
    /// Get a list of all supported languages.
    /// </summary>
    /// <returns>A list of all supported languages.</returns>
    [HttpGet("SupportedLanguages")]
    public ActionResult<List<LanguageDetails>> GetAllSupportedLanguages() =>
        Languages.AllNamingLanguages.Select(a => new LanguageDetails(a.Language)).ToList();
}
