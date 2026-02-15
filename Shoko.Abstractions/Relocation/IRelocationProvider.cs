using System;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Relocation;

/// <summary>
/// The base interface for any renamer.
/// </summary>
public interface IRelocationProvider
{
    /// <summary>
    ///   Friendly name of the relocation provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the relocation provider.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the relocation provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   This should be true if the renamer supports operating on unrecognized
    ///   files.
    /// </summary>
    bool SupportsUnrecognized { get => false; }

    /// <summary>
    ///   This should be true if the renamer can handle cases where part of or
    ///   all the metadata for the episodes and/or series linked to the video
    ///   is missing.
    /// </summary>
    bool SupportsIncompleteMetadata { get => false; }

    /// <summary>
    ///   Indicates that the renamer supports moving files. That is, changing
    ///   the directory the file is in.
    /// </summary>
    bool SupportsMoving { get => true; }

    /// <summary>
    ///   Indicates that the renamer supports renaming files. That is, changing
    ///   the name of the file itself.
    /// </summary>
    bool SupportsRenaming { get => true; }

    /// <summary>
    ///   Get the optimal path for where to place the file after relocation.
    /// </summary>
    /// <param name="context">
    ///   The context for the relocation. Contains most if not all related data
    ///   a relocation provider could want when determining the most optimal
    ///   path. See <see cref="RelocationContext"/> for details.
    /// </param>
    /// <returns>
    ///   A relocation result. See <see cref="RelocationResult"/> for details.
    /// </returns>
    RelocationResult GetPath(RelocationContext context)
        => RelocationResult.FromError(new NotImplementedException());
}

/// <summary>
/// A renamer with a settings model.
/// </summary>
/// <typeparam name="TConfig">Type of the settings model</typeparam>
public interface IRelocationProvider<TConfig> : IRelocationProvider where TConfig : IRelocationProviderConfiguration
{
    /// <summary>
    /// Get the new path for moving and/or renaming. See <see cref="RelocationResult"/> and its <see cref="RelocationResult.Error"/> for details on the return value.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    RelocationResult GetPath(RelocationContext<TConfig> context);
}
