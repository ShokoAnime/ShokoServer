using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents the outcome of a file relocation.
/// </summary>
/// <remarks>
/// Possible states of the outcome:
///   Success: Success set to true, with valid ImportFolder and RelativePath
///     values.
///
///   Failure: Success set to false and ErrorMessage containing the reason.
///     ShouldRetry may be set to true if the operation can be retried.
/// </remarks>
public record RelocationResult
{
    /// <summary>
    /// The relocation was successful.
    /// If true then the <see cref="ImportFolder"/> and
    /// <see cref="RelativePath"/> should be set to valid values, otherwise
    /// if false then the <see cref="ErrorMessage"/> should be set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportFolder), nameof(RelativePath))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool Success { get; set; } = false;

    /// <summary>
    /// True if the operation should be retried. This is more of an internal
    /// detail.
    /// </summary>
    internal bool ShouldRetry { get; set; } = false;

    /// <summary>
    /// Error message if the operation was not successful.
    /// </summary>
    public string? ErrorMessage { get; set; } = null;

    /// <summary>
    /// Exception thrown when it erred out.
    /// </summary>
    public Exception? Exception { get; set; } = null;

    /// <summary>
    /// Indicates the file was moved.
    /// </summary>
    public bool Moved { get; set; } = false;

    /// <summary>
    /// Indicates the file was renamed.
    /// </summary>
    public bool Renamed { get; set; } = false;

    /// <summary>
    /// The destination import folder if the relocation result were
    /// successful.
    /// </summary>
    public SVR_ImportFolder? ImportFolder { get; set; } = null;

    /// <summary>
    /// The relative path from the <see cref="ImportFolder"/> to where
    /// the file resides.
    /// </summary>
    public string? RelativePath { get; set; } = null;

    /// <summary>
    /// Helper to get the full server path if the relative path and import
    /// folder are valid.
    /// </summary>
    /// <returns>The combined path.</returns>
    internal string? AbsolutePath
        => Success && !string.IsNullOrEmpty(RelativePath) ? Path.Combine(ImportFolder.ImportFolderLocation, RelativePath) : null;
}
