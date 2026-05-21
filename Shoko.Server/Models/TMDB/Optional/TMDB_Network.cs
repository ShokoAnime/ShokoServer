using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

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

    public IReadOnlyList<TMDB_Show_Network> GetTmdbNetworkCrossReferences() =>
        RepoFactory.TMDB_Show_Network.GetByTmdbNetworkID(TmdbNetworkID);

    #endregion

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Network;

    int IMetadata<int>.ID => TmdbNetworkID;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #region IWithPrimaryImage Implementation

    public IImage? DefaultPrimaryImage
        => GetImages(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => GetImageCrossReferences(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

    #endregion

    #region ITmdbNetwork Implementation

    IReadOnlyList<ITmdbShow> ITmdbNetwork.Shows => GetTmdbNetworkCrossReferences()
        .Select(x => x.GetTmdbShow())
        .WhereNotNull()
        .ToList();

    #endregion
}
