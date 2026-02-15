using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Network : ITmdbNetwork
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_NetworkID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    public int TmdbNetworkID { get; set; }

    /// <summary>
    /// Main name of the network on TMDB.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the network originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    /// <summary>
    /// When the network was last linked to a show in the local system.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    #endregion

    #region Constructors

    #endregion

    #region Methods

    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbNetworkIDAndType(TmdbNetworkID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbNetworkID(TmdbNetworkID);

    public IReadOnlyList<TMDB_Show_Network> GetTmdbNetworkCrossReferences() =>
        RepoFactory.TMDB_Show_Network.GetByTmdbNetworkID(TmdbNetworkID);

    #endregion

    #region IMetadata Implementation

    int IMetadata<int>.ID => TmdbNetworkID;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithImages Implementation

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => GetImages(entityType);

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => entityType is ImageEntityType.Logo ? GetImages(ImageEntityType.Logo).LastOrDefault() : null;

    #endregion

    #region IWithPortraitImage Implementation

    IImage? IWithPortraitImage.PortraitImage => GetImages(ImageEntityType.Logo).LastOrDefault();

    #endregion

    #region ITmdbNetwork Implementation

    IReadOnlyList<ITmdbShow> ITmdbNetwork.Shows => GetTmdbNetworkCrossReferences()
        .Select(x => x.GetTmdbShow())
        .WhereNotNull()
        .ToList();

    #endregion
}
