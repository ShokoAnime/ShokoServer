namespace Shoko.Models.Enums
{
    public enum GroupFilterOperator
    {
        Include = 1,
        Exclude = 2,
        GreaterThan = 3,
        LessThan = 4,
        Equals = 5,
        NotEquals = 6,
        In = 7,
        NotIn = 8,
        LastXDays = 9,
        InAllEpisodes = 10,
        NotInAllEpisodes = 11
    }
}