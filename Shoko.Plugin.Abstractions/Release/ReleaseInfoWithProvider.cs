using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release info with provider name and creation date attached.
/// </summary>
public class ReleaseInfoWithProvider : ReleaseInfo, IReleaseInfo
{
    /// <summary>
    /// When the release information was last updated locally.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; } = null;

    /// <inheritdoc />
    public ReleaseInfoWithProvider(string providerName) : base()
    {
        ProviderName = providerName;
    }

    /// <inheritdoc />
    public ReleaseInfoWithProvider(ReleaseInfo releaseInfo, string providerName) : base(releaseInfo)
    {
        if (string.IsNullOrEmpty(ProviderName))
            ProviderName = providerName;
    }

    /// <inheritdoc />
    public ReleaseInfoWithProvider(IReleaseInfo releaseInfo) : base(releaseInfo)
    {
        LastUpdatedAt = releaseInfo.LastUpdatedAt;
    }

    #region IReleaseInfo Implementation

    string IReleaseInfo.ProviderName => ProviderName ?? string.Empty;

    IReadOnlyList<IHashDigest>? IReleaseInfo.Hashes => Hashes;

    IReleaseGroup? IReleaseInfo.Group => Group;

    IReleaseMediaInfo? IReleaseInfo.MediaInfo => MediaInfo;

    IReadOnlyList<IReleaseVideoCrossReference> IReleaseInfo.CrossReferences => CrossReferences;

    DateTime IReleaseInfo.LastUpdatedAt => LastUpdatedAt ?? CreatedAt;

    #endregion
}
