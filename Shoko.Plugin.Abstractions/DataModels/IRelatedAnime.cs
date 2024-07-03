
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IRelatedAnime
{
    int RelatedAnimeID { get; }
    ISeries? RelatedAnime { get; }
    RelationType RelationType { get; }
}
