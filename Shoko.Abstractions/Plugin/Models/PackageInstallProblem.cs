using System;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Why an install plan cannot be carried out.
/// </summary>
public sealed class PackageInstallProblem
{
    /// <summary>
    ///   The plugin the problem is with.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The plugin's name, if it is known.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    ///   The version range asked for, when the problem is with a dependency.
    /// </summary>
    public required string? VersionRange { get; init; }

    /// <summary>
    ///   The plugin asking for it, when the problem is with a dependency.
    /// </summary>
    public required Guid? RequiredBy { get; init; }

    /// <summary>
    ///   What is wrong, in a sentence.
    /// </summary>
    public required string Reason { get; init; }
}
