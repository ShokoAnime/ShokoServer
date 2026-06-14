using System;
using Newtonsoft.Json;

#nullable enable
namespace Shoko.Server.API.v3.Models.Auth;

/// <summary>
/// An API key.
/// </summary>
public class ApiToken(int userID, string username, string device, DateTime? expiresAt, string? token = null)
{
    /// <summary>
    /// The ID of the user the API key belongs to.
    /// </summary>
    public int UserID { get; set; } = userID;

    /// <summary>
    /// The username of the user the API key belongs to.
    /// </summary>
    public string Username { get; set; } = username;

    /// <summary>
    /// The device the API key was created for.
    /// </summary>
    public string Device { get; set; } = device;

    /// <summary>
    /// When the API key expires, or null if it never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; } = expiresAt;

    /// <summary>
    /// The API key's authorization token.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Token { get; set; } = token;
}
