using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.User;

public class HasWatchedEpisodesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override bool Evaluate(IUserDependentFilterable filterable) => filterable.WatchedEpisodes > 0;
}
