using System;
using System.Text;
using System.Xml.Serialization;

namespace Shoko.Abstractions.User;

/// <summary>
///   Represents an API token with its associated metadata.
/// </summary>
/// <param name="User">
///   The the user the token belongs to.
/// </param>
/// <param name="Device">
///   The device name the token is registered to.
/// </param>
/// <param name="Token">
///   The API token value. It is left out when the record is serialized, so
///   the key never ends up in a log or a stored copy by accident.
/// </param>
/// <param name="ExpiresAt">
///   The optional expiration time, or <c>null</c> if it never expires.
/// </param>
public record ApiToken(
    IUser User,
    string Device,
    [property: System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore, XmlIgnore] string Token,
    DateTime? ExpiresAt
)
{
    /// <summary>
    ///   The token's ID: stable for as long as the token exists, and never
    ///   given to another. <c>0</c> for a token the server does not store,
    ///   such as one a plugin makes to act as a user.
    /// </summary>
    public int ID { get; init; }

    /// <summary>
    ///   When the token was made, in the server's local time as
    ///   <see cref="ExpiresAt"/> is, or <c>null</c> when it is not known, as
    ///   for a token made before the server kept it.
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    ///   When the token was last used, in the server's local time, to within
    ///   five minutes: a use is only written once the stored time is that
    ///   old. <c>null</c> when it was not used since the server kept it.
    /// </summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>
    ///   Writes the token's members for <see cref="object.ToString"/>, leaving
    ///   out <see cref="Token"/>, so a token written to a log or into a message
    ///   never carries its key.
    /// </summary>
    /// <param name="builder">
    ///   The builder the members are written to.
    /// </param>
    /// <returns>
    ///   <c>true</c>, as members were written.
    /// </returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ID = ").Append(ID)
            .Append(", User = ").Append(User.Username)
            .Append(", Device = ").Append(Device)
            .Append(", ExpiresAt = ").Append(ExpiresAt?.ToString("O") ?? "never")
            .Append(", CreatedAt = ").Append(CreatedAt?.ToString("O") ?? "unknown")
            .Append(", LastUsedAt = ").Append(LastUsedAt?.ToString("O") ?? "unknown");
        return true;
    }
}
