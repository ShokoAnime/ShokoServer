using Shoko.Server.Settings;

namespace Shoko.Server.API;

public class RestController : BaseController
{
    public RestController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
