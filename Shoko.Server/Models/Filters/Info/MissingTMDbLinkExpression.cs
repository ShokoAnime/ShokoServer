using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Info;

/// <summary>
/// Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTMDbLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.HasMissingTMDbLink;
}
