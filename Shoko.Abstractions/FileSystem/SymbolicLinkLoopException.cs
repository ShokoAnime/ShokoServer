using System.IO;

namespace Shoko.Abstractions.FileSystem;

/// <summary>
///   Thrown when following a symbolic link leads back to a link already
///   followed, or through more links than a chain may have.
/// </summary>
public sealed class SymbolicLinkLoopException : IOException
{
    /// <summary>
    ///   The path the walk started from.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///   The link the walk reached a second time, or the last one followed
    ///   when the chain was too long.
    /// </summary>
    public string LoopPath { get; }

    /// <summary>
    ///   Creates the exception.
    /// </summary>
    /// <param name="path">The path the walk started from.</param>
    /// <param name="loopPath">Where the loop closed, or where the walk stopped.</param>
    /// <param name="tooLong">Whether the chain was too long rather than a loop.</param>
    public SymbolicLinkLoopException(string path, string loopPath, bool tooLong = false)
        : base(tooLong
            ? $"Following the symbolic links from {path} went through too many links, stopping at {loopPath}."
            : $"Following the symbolic links from {path} loops back to {loopPath}.")
    {
        Path = path;
        LoopPath = loopPath;
    }
}
