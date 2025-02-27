using System.Collections.Generic;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing;

public class HashingSettings
{
    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    public required bool ParallelMode { get; init; }

    /// <summary>
    ///   Gets the set of all available hash types across all providers.
    /// </summary>
    public required IReadOnlySet<string> AllAvailableHashTypes { get; init; }

    /// <summary>
    ///   Gets the set of all enabled hash types across all providers.
    /// </summary>
    public required IReadOnlySet<string> AllEnabledHashTypes { get; init; }

    public static class Input
    {
        public class UpdateHashingSettingsBody
        {
            /// <summary>
            ///   Gets or sets a value indicating whether to use parallel mode.
            /// </summary>
            public bool? ParallelMode { get; init; }
        }
    }
}
