using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.Settings;

using UserConfigurationInfo = Shoko.Server.API.v3.Models.Shoko.UserConfigurationInfo;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The user configurations kept per user.
/// </summary>
/// <param name="userConfigurationService">Keeps the configurations.</param>
/// <param name="userService">Finds the users.</param>
/// <param name="settingsProvider">The settings provider.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/User")]
[ApiV3]
[Authorize]
public class UserConfigurationController(
    IUserConfigurationService userConfigurationService,
    IUserService userService,
    ISettingsProvider settingsProvider
) : BaseController(settingsProvider)
{
    private const string UserByIdNotFound = "An user by the given `userID` doesn't exist.";

    private const string ConfigurationNotFound = "No configuration is stored for the given `configurationID`.";

    #region Current User

    /// <summary>
    /// List the registered configurations and the ones stored for the
    /// current user.
    /// </summary>
    /// <returns>The configurations.</returns>
    [HttpGet("Current/Configuration")]
    public ActionResult<List<UserConfigurationInfo>> GetCurrentUserConfigurationInfo()
        => ListInfo(User);

    /// <summary>
    /// Get one of the current user's configurations.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration.</returns>
    [HttpGet("Current/Configuration/{configurationID}")]
    [ProducesResponseType<JObject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetCurrentUserConfiguration([FromRoute] string configurationID)
        => Get(User, configurationID);

    /// <summary>
    /// Replace one of the current user's registered configurations.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <param name="configuration">The configuration, a JSON object.</param>
    /// <returns>The stored configuration.</returns>
    [HttpPut("Current/Configuration/{configurationID}")]
    [ProducesResponseType<JObject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult PutCurrentUserConfiguration([FromRoute] string configurationID, [FromBody] JObject configuration)
        => Save(User, configurationID, configuration);

    /// <summary>
    /// Delete one of the current user's configurations.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>Nothing.</returns>
    [HttpDelete("Current/Configuration/{configurationID}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteCurrentUserConfiguration([FromRoute] string configurationID)
        => Delete(User, configurationID);

    #endregion

    #region Any User

    /// <summary>
    /// List the registered configurations and the ones stored for a user.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">The user's ID.</param>
    /// <returns>The configurations.</returns>
    [Authorize("admin")]
    [HttpGet("{userID:int}/Configuration")]
    public ActionResult<List<UserConfigurationInfo>> GetUserConfigurationInfo([FromRoute, Range(1, int.MaxValue)] int userID)
        => userService.GetUserByID(userID) is { } user ? ListInfo(user) : NotFound(UserByIdNotFound);

    /// <summary>
    /// Get one of a user's configurations.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">The user's ID.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The configuration.</returns>
    [Authorize("admin")]
    [HttpGet("{userID:int}/Configuration/{configurationID}")]
    [ProducesResponseType<JObject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetUserConfiguration([FromRoute, Range(1, int.MaxValue)] int userID, [FromRoute] string configurationID)
        => userService.GetUserByID(userID) is { } user ? Get(user, configurationID) : NotFound(UserByIdNotFound);

    /// <summary>
    /// Replace one of a user's registered configurations.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">The user's ID.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <param name="configuration">The configuration, a JSON object.</param>
    /// <returns>The stored configuration.</returns>
    [Authorize("admin")]
    [HttpPut("{userID:int}/Configuration/{configurationID}")]
    [ProducesResponseType<JObject>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult PutUserConfiguration(
        [FromRoute, Range(1, int.MaxValue)] int userID,
        [FromRoute] string configurationID,
        [FromBody] JObject configuration
    )
        => userService.GetUserByID(userID) is { } user ? Save(user, configurationID, configuration) : NotFound(UserByIdNotFound);

    /// <summary>
    /// Delete one of a user's configurations.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">The user's ID.</param>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>Nothing.</returns>
    [Authorize("admin")]
    [HttpDelete("{userID:int}/Configuration/{configurationID}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteUserConfiguration([FromRoute, Range(1, int.MaxValue)] int userID, [FromRoute] string configurationID)
        => userService.GetUserByID(userID) is { } user ? Delete(user, configurationID) : NotFound(UserByIdNotFound);

    #endregion

    #region Registrations

    /// <summary>
    /// List the registered user configurations.
    /// </summary>
    /// <returns>The registered configurations.</returns>
    [HttpGet("Configuration")]
    public ActionResult<List<UserConfigurationInfo>> GetRegisteredConfigurationInfo()
        => userConfigurationService.GetAllInfos().Select(info => new UserConfigurationInfo(info)).ToList();

    /// <summary>
    /// Get the schema registered configurations are checked against.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The schema.</returns>
    [HttpGet("Configuration/{configurationID}/Schema")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetSchema([FromRoute] string configurationID)
        => userConfigurationService.GetSchema(configurationID) is { } schema
            ? Content(schema, "application/json")
            : NotFound("The given `configurationID` is not registered, has no schema, or its schema cannot be read.");

    #endregion

    #region Helpers

    [NonAction]
    private ActionResult<List<UserConfigurationInfo>> ListInfo(IUser user)
        => userConfigurationService.GetInfoForUser(user).Select(info => new UserConfigurationInfo(info)).ToList();

    [NonAction]
    private ActionResult Get(IUser user, string configurationID)
        => userConfigurationService.LoadMasked(user, configurationID) is { } configuration
            ? Content(configuration, "application/json")
            : NotFound(ConfigurationNotFound);

    [NonAction]
    private ActionResult Save(IUser user, string configurationID, JObject configuration)
    {
        try
        {
            userConfigurationService.Save(user, configurationID, configuration.ToString(Formatting.None));
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }

        return Get(user, configurationID);
    }

    [NonAction]
    private ActionResult Delete(IUser user, string configurationID)
        => userConfigurationService.Delete(user, configurationID) ? NoContent() : NotFound(ConfigurationNotFound);

    #endregion
}
