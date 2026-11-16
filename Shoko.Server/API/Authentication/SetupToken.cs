using System.Security.Cryptography;
using System.Text;

namespace Shoko.Server.API.Authentication;

/// <summary>
///   The setup token, which setup mode and failure mode take as the API key
///   of every request while it is set.
/// </summary>
/// <param name="token">The setup token. Empty or <c>null</c> requires none.</param>
public sealed class SetupToken(string? token)
{
    private readonly byte[]? _token = string.IsNullOrEmpty(token) ? null : Encoding.UTF8.GetBytes(token);

    /// <summary>
    ///   Whether setup mode and failure mode require the setup token.
    /// </summary>
    public bool IsRequired => _token is not null;

    /// <summary>
    ///   Whether a key given with a request is the setup token.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><c>true</c> when it is.</returns>
    public bool Matches(string? key)
        => _token is not null && key is { Length: > 0 } &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), _token);
}
