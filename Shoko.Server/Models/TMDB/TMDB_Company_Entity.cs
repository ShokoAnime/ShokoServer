
#nullable enable
using System;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Company_Entity
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Company_EntityID { get; set; }

    /// <summary>
    /// TMDB Company ID.
    /// </summary>
    public int TmdbCompanyID { get; set; }

    /// <summary>
    /// TMDB Entity Type.
    /// </summary>
    public ForeignEntityType TmdbEntityType { get; set; }

    /// <summary>
    /// TMDB Entity ID.
    /// </summary>
    public int TmdbEntityID { get; set; }

    /// <summary>
    /// Used for ordering the companies for the entity.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    /// Used for ordering the entities for the company.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    #endregion

    #region Constructors

    public TMDB_Company_Entity() { }

    public TMDB_Company_Entity(int companyId, ForeignEntityType entityType, int entityId, int index, DateOnly? releasedAt)
    {
        TmdbCompanyID = companyId;
        TmdbEntityType = entityType;
        TmdbEntityID = entityId;
        Ordering = index;
        ReleasedAt = releasedAt;
    }

    #endregion

    #region Methods

    public TMDB_Company? Company { get; set; }

    public TMDB_Show? TVShow { get; set; }
    public TMDB_Movie? Movie { get; set; }

    #endregion
}
