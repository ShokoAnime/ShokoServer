using System;

namespace Shoko.Abstractions.Plugin.Exceptions;

/// <summary>
///   Exception thrown when a package archive's download, or its files once
///   unpacked, are not the size its manifest gives.
/// </summary>
public sealed class InvalidArchiveSizeException : Exception
{
    /// <summary>
    ///   Whether the unpacked size failed; otherwise the packed size, the
    ///   download's own, did.
    /// </summary>
    public bool IsUnpackedSize { get; }

    /// <summary>
    ///   The size the manifest gives, in bytes.
    /// </summary>
    public long ExpectedSize { get; }

    /// <summary>
    ///   The actual size, in bytes: what the server announced or what was
    ///   read for the download, or what was written when unpacking, up to the
    ///   point it went past the expected size.
    /// </summary>
    public long ActualSize { get; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="InvalidArchiveSizeException"/> class.
    /// </summary>
    /// <param name="expected">
    ///   The size the manifest gives, in bytes.
    /// </param>
    /// <param name="actual">
    ///   The actual size, in bytes.
    /// </param>
    /// <param name="isUnpackedSize">
    ///   Whether the unpacked size failed, rather than the packed one.
    /// </param>
    public InvalidArchiveSizeException(long expected, long actual, bool isUnpackedSize = false)
    : base($"Package archive {(isUnpackedSize ? "unpacked" : "packed")} size verification failed. Expected: {expected} bytes, Actual: {actual} bytes")
    {
        IsUnpackedSize = isUnpackedSize;
        ExpectedSize = expected;
        ActualSize = actual;
    }
}
