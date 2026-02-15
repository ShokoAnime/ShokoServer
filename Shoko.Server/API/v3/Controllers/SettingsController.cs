using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Web.Attributes;
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
public class SettingsController(ISettingsProvider settingsProvider, ConfigurationProvider<ServerSettings> configurationProvider, ILogger<SettingsController> logger, IUDPConnectionHandler udpHandler) : BaseController(settingsProvider)
{
    private readonly ConfigurationProvider<ServerSettings> _configurationProvider = configurationProvider;

    private readonly IUDPConnectionHandler _udpHandler = udpHandler;

    private readonly ILogger<SettingsController> _logger = logger;

    // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
    // Some may be patched to "", though.

    // TODO some way of distinguishing what a normal user vs an admin can set.

    /// <summary>
    /// Get all settings
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IServerSettings> GetSettings()
        => new(SettingsProvider.GetSettings());

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
            settings.ApplyTo(existingSettings, ModelState);
            SettingsProvider.SaveSettings(existingSettings);
            return Ok();
        }
        catch (ConfigurationValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    /// Tests a Login with the given Credentials. This does not save the credentials.
    /// </summary>
    /// <param name="credentials">POST the body as a <see cref="Credentials"/> object</param>
    /// <returns></returns>
    [HttpPost("AniDB/TestLogin")]
    public ActionResult TestAniDB([FromBody] Credentials credentials)
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

        var settings = SettingsProvider.GetSettings();
        if (!_udpHandler.IsAlive)
            _udpHandler.Init(credentials.Username, credentials.Password, settings.AniDb.UDPServerAddress, settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);
        else _udpHandler.ForceLogout();

        if (!_udpHandler.TestLogin(credentials.Username, credentials.Password))
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
