
using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Hashing;

/// <summary>
/// Contains information about a <see cref="IHashProvider"/>.
/// </summary>
public class HashProviderInfo
{
    /// <summary>
    /// The unique ID of the provider, generated off of the full class name.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    /// The <see cref="IHashProvider"/> that this info is for.
    /// </summary>
    public required IHashProvider Provider { get; init; }

    /// <summary>
    /// The enabled hash types.
    /// </summary>
    public required HashSet<string> EnabledHashTypes { get; set; }

    /// <summary>
    /// The priority of the hash provider when running in sequential mode.
    /// </summary>
    public required int Priority { get; set; }
}
