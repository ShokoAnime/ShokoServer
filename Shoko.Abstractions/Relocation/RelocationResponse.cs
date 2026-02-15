using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Relocation;

/// <summary>
/// Represents the outcome of a file relocation.
/// </summary>
/// <remarks>
/// Possible states of the outcome:
///   Success: Success set to true, with valid <see cref="ManagedFolder"/> and <see cref="RelativePath"/>
///     values.
///
///   Failure: Success set to false and ErrorMessage containing the reason.
///     ShouldRetry may be set to true if the operation can be retried.
/// </remarks>
public record RelocationResponse
{
    /// <summary>
    /// The relocation was successful.
    /// If true then the <see cref="ManagedFolder"/> and
    /// <see cref="RelativePath"/> should be set to valid values, otherwise
    /// if false then the <see cref="Error"/> should be set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ManagedFolder), nameof(RelativePath))]
    [MemberNotNullWhen(false, nameof(Error))]
    public required bool Success { get; set; } = false;

    /// <summary>
    /// The destination managed folder if the relocation result were
    /// successful.
    /// </summary>
    public IManagedFolder? ManagedFolder { get; init; } = null;

    /// <summary>
    /// The relative path from the <see cref="ManagedFolder"/> to where
    /// the file resides.
    /// </summary>
    public string? RelativePath { get; init; } = null;

    /// <summary>
    /// Helper to get the full video file path if the relative path and import
    /// folder are valid.
    /// </summary>
    /// <returns>The combined path.</returns>
    public string? AbsolutePath
        => Success && !string.IsNullOrEmpty(RelativePath) ? Path.Combine(ManagedFolder.Path, RelativePath) : null;

    /// <summary>
    /// Set this object if the event is not successful. If this is set, it
    /// assumed that there was a failure, and the rename/move operation should
    /// be aborted. It is required to have at least a message.
    ///
    /// An exception can be provided if relevant.
    /// </summary>
    public RelocationError? Error { get; init; }

    /// <summary>
    /// Indicates the file was moved.
    /// </summary>
    public bool Moved { get; init; } = false;

    /// <summary>
    /// Indicates the file was renamed.
    /// </summary>
    public bool Renamed { get; init; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationResponse"/> class.
    /// </summary>
    private RelocationResponse() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationResponse"/> class
    /// indicating success with the relocation operation.
    /// </summary>
    /// <param name="managedFolder">The managed folder where the file was relocated.</param>
    /// <param name="relativePath">The relative path from the managed folder to where the file resides.</param>
    /// <param name="moved">Indicates the file was moved.</param>
    /// <param name="renamed">Indicates the file was renamed.</param>
    /// <returns>A new instance of the <see cref="RelocationResponse"/> class.</returns>
    public static RelocationResponse FromResult(IManagedFolder managedFolder, string relativePath, bool moved = false, bool renamed = false)
        => new()
        {
            Success = true,
            ManagedFolder = managedFolder,
            RelativePath = relativePath,
            Moved = moved,
            Renamed = renamed,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationResponse"/> class from
    /// a relocation error.
    /// </summary>
    /// <param name="error">The relocation error.</param>
    /// <returns>A <see cref="RelocationResponse"/> with the error set.</returns>
    public static RelocationResponse FromError(RelocationError error) => new() { Success = false, Error = error };

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationResponse"/> class from
    /// an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The exception that caused the relocation operation to fail.</param>
    /// <returns>A <see cref="RelocationResponse"/> with the error set.</returns>
    public static RelocationResponse FromError(string message, Exception? exception = null)
        => new() { Success = false, Error = new(message, exception) };

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocationResponse"/> class from an
    /// exception.
    /// </summary>
    /// <param name="exception ">The exception that caused the relocation operation to fail.</param>
    /// <returns>A <see cref="RelocationResponse"/> with the exception set.</returns>
    public static RelocationResponse FromError(Exception exception)
        => new() { Success = false, Error = new(exception) };
}
