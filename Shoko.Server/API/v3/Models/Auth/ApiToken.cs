using System;
using Newtonsoft.Json;

using AbstractApiToken = Shoko.Abstractions.User.ApiToken;

namespace Shoko.Server.API.v3.Models.Auth;

/// <summary>
/// An API key.
/// </summary>
public class ApiToken(AbstractApiToken apiToken, bool includeToken = false)
{
    /// <summary>
    /// The API key's ID: stable for as long as it exists, and never given to
    /// another. Revoke it with <c>DELETE /api/v3/Auth/Token/{tokenID}</c>.
    /// </summary>
    public int ID { get; set; } = apiToken.ID;

    /// <summary>
    /// The ID of the user the API key belongs to.
    /// </summary>
    public int UserID { get; set; } = apiToken.User.LocalID;

    /// <summary>
    /// The username of the user the API key belongs to.
    /// </summary>
    public string Username { get; set; } = apiToken.User.Username;

    /// <summary>
    /// The device the API key was created for.
    /// </summary>
    public string Device { get; set; } = apiToken.Device;

    /// <summary>
    /// When the API key expires, or null if it never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; } = apiToken.ExpiresAt;

    /// <summary>
    /// When the API key was created, or null when it is not known, as for a
    /// key created before the server kept it.
    /// </summary>
    public DateTime? CreatedAt { get; set; } = apiToken.CreatedAt;

    /// <summary>
    /// When the API key was last used, to within five minutes, or null when it
    /// was not used since the server kept it.
    /// </summary>
    public DateTime? LastUsedAt { get; set; } = apiToken.LastUsedAt;

    /// <summary>
    /// The API key's authorization token.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Token { get; set; } = includeToken ? apiToken.Token : null;
}
