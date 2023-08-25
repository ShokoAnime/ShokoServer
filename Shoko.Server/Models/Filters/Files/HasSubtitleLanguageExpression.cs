using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Files;

public class HasSubtitleLanguageExpression : FilterExpression
{
    public string Parameter { get; set; }
    public override bool UserDependent => false;
    public override bool Evaluate(IFilterable filterable) => filterable.SubtitleLanguages.Contains(Parameter);
}
