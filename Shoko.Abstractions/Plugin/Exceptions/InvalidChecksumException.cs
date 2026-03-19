using System;

namespace Shoko.Abstractions.Plugin.Exceptions;

/// <summary>
///   Exception thrown when package checksum verification fails.
/// </summary>
public sealed class InvalidChecksumException : Exception
{
    /// <summary>
    ///   The expected checksum.
    /// </summary>
    public string ExpectedChecksum { get; }

    /// <summary>
    ///   The actual checksum.
    /// </summary>
    public string ActualChecksum { get; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="InvalidChecksumException"/> class.
    /// </summary>
    /// <param name="expected">
    ///   The expected checksum.
    /// </param>
    /// <param name="actual">
    ///   The actual checksum.
    /// </param>
    public InvalidChecksumException(string expected, string actual)
    : base($"Package checksum verification failed. Expected: {expected}, Actual: {actual}")
    {
        ExpectedChecksum = expected;
        ActualChecksum = actual;
    }
}
