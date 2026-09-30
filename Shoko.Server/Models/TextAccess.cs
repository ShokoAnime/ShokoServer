using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Services;

namespace Shoko.Server.Models;

/// <summary>
///   Hands the text manager to the models, which are not built through
///   dependency injection, so they read their titles and overviews through it.
/// </summary>
internal static class TextAccess
{
    private static MetadataTextManager? _manager;

    /// <summary>
    ///   The text manager the models read through.
    /// </summary>
    /// <value>
    ///   The manager put in place with <see cref="Use"/>, or else the server's
    ///   own, looked up on first use.
    /// </value>
#pragma warning disable CS0618 // The models are not built through dependency injection.
    internal static MetadataTextManager Manager
        => _manager ??= (MetadataTextManager)ISystemService.StaticServices.GetRequiredService<IMetadataTextManager>();
#pragma warning restore CS0618

    /// <summary>
    ///   The manager in place, without looking the server's own up.
    /// </summary>
    internal static MetadataTextManager? Current => _manager;

    /// <summary>
    ///   Puts a manager in place for the models to read through and the
    ///   repositories to tell, as the server does with its own once built and
    ///   tests do with one of their own.
    /// </summary>
    /// <param name="manager">The manager, or <c>null</c> to look the server's own up again.</param>
    internal static void Use(MetadataTextManager? manager)
        => _manager = manager;

    /// <summary>
    ///   The manager in place, or the server's own once its services are up.
    /// </summary>
    /// <value>The manager, or <c>null</c> when none can be reached yet.</value>
    internal static MetadataTextManager? Reachable
        => _manager ?? (ISystemService.HasStaticServices ? Manager : null);

    /// <summary>
    ///   Tells the text manager in use, if any, that an entry changed, so what
    ///   was worked out from it is worked out again.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    internal static void Forget(MetadataGuid entityID)
        => _manager?.Invalidate(entityID);

    /// <summary>
    ///   Tells the text manager in use, if any, that what a filter matches an
    ///   entry by may have changed, with none of its texts.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    internal static void ForgetFilters(MetadataGuid entityID)
        => _manager?.InvalidateFilters(entityID);
}
