using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class LanguageDetails
{
    /// <summary>
    /// Language name.
    /// </summary>
    public string Name;

    /// <summary>
    /// Alpha 2 code, with `x-` extensions.
    /// </summary>
    public string Alpha2;

    public LanguageDetails(TitleLanguage language)
    {
        Name = language.GetDescription();
        Alpha2 = language.GetString();
    }
}
