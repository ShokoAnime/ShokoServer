namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTvDBLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.HasMissingTvDbLink;
    }
}
