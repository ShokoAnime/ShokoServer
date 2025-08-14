
namespace Shoko.Models.Enums;

public enum MatchRating
{
    UserVerified = 1,
    DateAndTitleMatches = 2, // Dates and Titles match, give or take
    DateMatches = 3, // Dates Matched
    TitleMatches = 4, // Titles Matched
    FirstAvailable = 5, // Neither Matched, but we could fill from adjacent episodes
    SarahJessicaParker = 6, // http://southpark.wikia.com/wiki/Sarah_Jessica_Parker
    TitleKindaMatches = 7, // Title is close, but not exact
    DateAndTitleKindaMatches = 8, // Date and Title are close, but not exact
}
