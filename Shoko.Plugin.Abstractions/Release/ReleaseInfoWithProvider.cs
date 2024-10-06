using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release info with provider name and creation date attached.
/// </summary>
internal class ReleaseInfoWithProvider : ReleaseInfo, IReleaseInfo
{
    /// <inheritdoc />
    public string ProviderID { get; set; } = string.Empty;

    /// <inheritdoc />
    public ReleaseInfoWithProvider() { }

    /// <inheritdoc />
    public ReleaseInfoWithProvider(ReleaseInfoWithProvider other) : base(other)
    {
        ProviderID = other.ProviderID;
    }

    #region IReleaseInfo Implementation

    IReleaseGroup? IReleaseInfo.Group => Group;

    IReleaseMediaInfo? IReleaseInfo.MediaInfo => MediaInfo;

    IReadOnlyList<IReleaseVideoCrossReference> IReleaseInfo.CrossReferences => CrossReferences;

    #endregion
}
