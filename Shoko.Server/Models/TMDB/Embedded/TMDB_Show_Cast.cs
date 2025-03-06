using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member within a show.
/// </summary>
public class TMDB_Show_Cast : TMDB_Cast, ICast<ISeries>
{
    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <inheritdoc/>
    public override int TmdbParentID => TmdbShowID;

    /// <summary>
    /// Number of episodes within this show the cast member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of season within this show the cast member have worked on.
    /// </summary>
    public int SeasonCount { get; set; }

    public TMDB_Show? GetTmdbShow() => RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public override IMetadata<int>? GetTmdbParent() => GetTmdbShow();

    ISeries? ICast<ISeries>.ParentOfType => GetTmdbShow();

}
