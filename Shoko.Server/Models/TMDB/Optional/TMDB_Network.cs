using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Network : ITmdbNetwork, IInlineTextSource
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

    #region Methods

    public IReadOnlyList<TMDB_Show_Network> GetTmdbNetworkCrossReferences() =>
        RepoFactory.TMDB_Show_Network.GetByTmdbNetworkID(TmdbNetworkID);

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(MetadataSource.TMDB, Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => null;

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Network, TmdbNetworkID.ToString());

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region ITmdbNetwork Implementation

    int ITmdbNetwork.TmdbID => TmdbNetworkID;

    IReadOnlyList<ITmdbShow> ITmdbNetwork.Shows => GetTmdbNetworkCrossReferences()
        .Select(x => x.GetTmdbShow())
        .WhereNotNull()
        .ToList();

    #endregion
}
