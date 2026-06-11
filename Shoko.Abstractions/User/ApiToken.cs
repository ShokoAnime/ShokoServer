using System;

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
///   The API token value.
/// </param>
/// <param name="ExpiresAt">
///   The optional expiration time, or <c>null</c> if it never expires.
/// </param>
public record ApiToken(
    IUser User,
    string Device,
    string Token,
    DateTime? ExpiresAt
);
