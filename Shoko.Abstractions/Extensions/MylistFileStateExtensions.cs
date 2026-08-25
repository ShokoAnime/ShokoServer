using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extensions for the <see cref="MylistFileState"/> enum.
/// </summary>
public static class MylistFileStateExtensions
{
    extension(MylistFileState state)
    {
        /// <summary>
        ///   Whether the state can be sent to AniDB over the UDP API.
        /// </summary>
        /// <remarks>
        ///   The UDP API validates the <c>filestate</c> parameter against the
        ///   list in its own definition, which predates the Blu-ray option the
        ///   web UI has since gained and never grew to include it. Sending a
        ///   value it does not know answers <c>505 ILLEGAL INPUT OR ACCESS
        ///   DENIED</c>, which is indistinguishable from being denied access,
        ///   so such a value is dropped rather than sent. Reading one back is
        ///   fine; only writing is refused.
        /// </remarks>
        public bool IsWritable => state is not MylistFileState.OnBluRay;
    }
}
