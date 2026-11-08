using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Plugin;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The licences of the third-party packages and assets the server ships: the core's and every loaded plugin's.
/// Signed-in users only, since the list tells what the server is built from.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
[Authorize(Roles = "user,admin")]
public class ThirdPartyNoticeController(ISettingsProvider settingsProvider, IThirdPartyNoticeService noticeService) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get the notices, one section per plugin that ships any: the core first, then the active plugins in load order.
    /// The texts are left out; fetch them by key with <see cref="GetTexts"/>.
    /// </summary>
    /// <returns>The sections.</returns>
    [HttpGet]
    public ActionResult<List<ThirdPartyNoticeSection>> GetSections()
        => noticeService.GetSections().Select(section => new ThirdPartyNoticeSection(section)).ToList();

    /// <summary>
    /// Get licence and notice texts by their keys, as the notices list them. A key no notices have is left out.
    /// </summary>
    /// <param name="key">The keys, separated by commas.</param>
    /// <returns>The texts, by key.</returns>
    [HttpGet("Text")]
    public ActionResult<Dictionary<string, string>> GetTexts([FromQuery, Required, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<string> key)
    {
        var texts = new Dictionary<string, string>();
        foreach (var textKey in key)
            if (noticeService.GetText(textKey) is { } text)
                texts[textKey] = text;
        return texts;
    }
}
