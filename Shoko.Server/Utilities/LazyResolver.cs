using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Server.Utilities;

/// <summary>
/// Resolves <see cref="Lazy{T}"/> from the container, so a service caught in a
/// dependency cycle can declare what it needs in its constructor instead of
/// injecting <see cref="IServiceProvider"/> and resolving by hand on first use.
/// </summary>
/// <remarks>
/// Registered as an open generic, so any <c>Lazy&lt;TService&gt;</c> is
/// resolvable for any <c>TService</c> the container knows. Initialisation is
/// thread-safe by virtue of <see cref="Lazy{T}"/> itself, which the hand-rolled
/// <c>??=</c> it replaces was not.
///
/// Only valid for singletons. The provider captured here is the one that
/// resolved the consumer, so a singleton consumer captures the root provider
/// and a scoped dependency resolved through it would outlive its scope. That is
/// equally true of the pattern this replaces.
/// </remarks>
public sealed class LazyResolver<T>(IServiceProvider provider) : Lazy<T>(provider.GetRequiredService<T>) where T : notnull;
