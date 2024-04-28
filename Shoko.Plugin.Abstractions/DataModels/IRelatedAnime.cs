
#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IRelatedAnime
{
    int RelatedAnimeID { get; }
    IAnime? RelatedAnime { get; }
    RelationType RelationType { get; }
}
