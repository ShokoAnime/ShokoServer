
#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseInfoSettings
{
    /// <summary>
    ///   Gets a value indicating whether to use parallel mode.
    /// </summary>
    public required bool ParallelMode { get; init; }

    public static class Input
    {
        public class UpdateReleaseInfoSettingsBody
        {
            /// <summary>
            ///   Sets a value indicating whether to use parallel mode.
            /// </summary>
            public bool? ParallelMode { get; init; }
        }
    }
}
