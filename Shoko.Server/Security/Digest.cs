using System;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace Shoko.Server;

/// <summary>
/// Simple wrapper class for generating message digests
/// </summary>
public class Digest
{
    /// <summary>
    /// Generate a message digest from the specified string using default SHA512 algo.
    /// </summary>
    /// <param name="source">source string to hash</param>
    /// <returns>message digest in hexadecimal form, or string.Empty if error occurs</returns>
    public static string Hash(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        var sourceBytes = Encoding.UTF8.GetBytes(source);
        var destBytes = SHA512.HashData(sourceBytes);
        return Convert.ToHexString(destBytes);
    }
}
