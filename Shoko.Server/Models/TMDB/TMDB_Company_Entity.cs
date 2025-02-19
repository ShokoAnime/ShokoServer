
#nullable enable
using System;
using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public abstract class TMDB_Company_Entity
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
    [NotMapped] // Discriminators cannot be mapped. They are automatically set from the type
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

    #region Methods

    public virtual TMDB_Company? Company { get; set; }

    public virtual TMDB_Show? TVShow { get; set; }
    public virtual TMDB_Movie? Movie { get; set; }

    #endregion
}

public class TMDB_Company_Show : TMDB_Company_Entity;
public class TMDB_Company_Movie : TMDB_Company_Entity;
