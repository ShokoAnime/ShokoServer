using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class AudioLanguageCountSelector : INumberSelector
{
    public bool UserDependent => false;
    public Func<IFilterable, double> Selector => f => f.AudioLanguages.Count;
}
