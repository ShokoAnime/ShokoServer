using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Shoko.Abstractions.Web;

/// <summary>
///   A file held in memory, for an <see cref="IFileProvider"/> that serves
///   content it builds or rewrites, such as an index with its paths filled in.
/// </summary>
/// <param name="_name">The file name, with its extension, which decides the content type served.</param>
/// <param name="_contents">The file contents.</param>
/// <param name="_lastModified">When the contents last changed. The time of creation when <c>null</c>.</param>
public sealed class MemoryFileInfo(string _name, byte[] _contents, DateTimeOffset? _lastModified = null) : IFileInfo
{
    /// <summary>
    ///   Creates a file from text, stored as UTF-8.
    /// </summary>
    /// <param name="name">The file name, with its extension, which decides the content type served.</param>
    /// <param name="text">The file contents.</param>
    /// <param name="lastModified">When the contents last changed. The time of creation when <c>null</c>.</param>
    public MemoryFileInfo(string name, string text, DateTimeOffset? lastModified = null)
        : this(name, Encoding.UTF8.GetBytes(text), lastModified) { }

    /// <summary>
    ///   Always <c>true</c>.
    /// </summary>
    public bool Exists => true;

    /// <summary>
    ///   Always <c>false</c>.
    /// </summary>
    public bool IsDirectory => false;

    /// <summary>
    ///   When the contents last changed, or when the file was created.
    /// </summary>
    public DateTimeOffset LastModified { get; } = _lastModified ?? DateTimeOffset.UtcNow;

    /// <summary>
    ///   The length of the contents in bytes.
    /// </summary>
    public long Length => _contents.Length;

    /// <summary>
    ///   The file name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    ///   Always <c>null</c>, as the file is not on disk.
    /// </summary>
    public string? PhysicalPath => null;

    /// <summary>
    ///   Opens a read-only stream over the contents.
    /// </summary>
    /// <returns>The stream.</returns>
    public Stream CreateReadStream()
        => new MemoryStream(_contents, writable: false);
}
