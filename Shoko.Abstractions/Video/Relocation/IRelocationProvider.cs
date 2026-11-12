using System;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Video.Relocation;

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
    ///   Whether you have what you need to answer, such as an API key or
    ///   credentials.
    /// </summary>
    /// <remarks>
    ///   Read every time the core is about to use you. While it is
    ///   <c>false</c>, relocating a file with you answers with an error
    ///   instead of calling you, and automatic relocation leaves the file
    ///   where it is. The default is <c>true</c>.
    /// </remarks>
    bool IsConfigured { get => true; }

    /// <summary>
    ///   What you are missing while <see cref="IsConfigured"/> is
    ///   <c>false</c>, in words a person can read, such as "No API key is
    ///   set.", or <c>null</c> to give no reason.
    /// </summary>
    /// <remarks>
    ///   Shown in the provider's availability. Not read while you are
    ///   configured. The default is <c>null</c>.
    /// </remarks>
    string? NotConfiguredReason { get => null; }

    /// <summary>
    ///   This should be true if the renamer supports operating on
    ///   unrecognized files: linked to nothing, with neither an AniDB link nor
    ///   a stored release (<see cref="IVideo.IsRecognized"/> is false).
    /// </summary>
    bool SupportsUnrecognized { get => false; }

    /// <summary>
    ///   This should be true if the renamer supports operating on recognized
    ///   files with no AniDB link: their stored release links them only to
    ///   entries of other sources, which it reads through
    ///   <see cref="IVideo.MetadataCrossReferences"/>, while the AniDB-linked
    ///   episodes, series and groups of the context are empty. Such a file is
    ///   not unrecognized, so <see cref="SupportsUnrecognized"/> does not let
    ///   it through.
    /// </summary>
    bool SupportsNonAnidbLinkedVideos { get => false; }

    /// <summary>
    ///   This should be true if the renamer can handle a video in import
    ///   limbo (<see cref="IVideo.IsInImportLimbo"/>): one with an entry it is
    ///   linked to, of any source, not stored yet.
    /// </summary>
    bool SupportsIncompleteMetadata { get => false; }

    /// <summary>
    ///   Indicates that the renamer supports moving files. That is, changing
    ///   the directory the file is in.
    /// </summary>
    /// <remarks>
    ///   When <c>false</c>, the server sets
    ///   <see cref="RelocationResult.SkipMove"/> on every result the provider
    ///   returns, so the file keeps its current folder whatever the result
    ///   says.
    /// </remarks>
    bool SupportsMoving { get => true; }

    /// <summary>
    ///   Indicates that the renamer supports renaming files. That is, changing
    ///   the name of the file itself.
    /// </summary>
    /// <remarks>
    ///   When <c>false</c>, the server sets
    ///   <see cref="RelocationResult.SkipRename"/> on every result the provider
    ///   returns, so the file keeps its current name whatever the result
    ///   says.
    /// </remarks>
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
    RelocationResult GetPath(RelocationContext context);
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

    RelocationResult IRelocationProvider.GetPath(RelocationContext context)
        => context is RelocationContext<TConfig> configuredContext
            ? GetPath(configuredContext)
            : RelocationResult.FromError(
                new ArgumentException($"The relocation provider needs a context with its {typeof(TConfig).Name} configuration.", nameof(context))
            );
}
