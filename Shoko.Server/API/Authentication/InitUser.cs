using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.API.Authentication;

// This is a fake user to aid in Authentication during first run
public class InitUser : ShokoUser, IIdentity
{
    public static InitUser Instance { get; } = new();

    private InitUser()
    {
        ID = 0;
        Username = "init";
        Password = "";
        IsAdmin = 1;
        HideCategories = "";
    }

    [NotMapped] string IIdentity.AuthenticationType => "API";

    [NotMapped] bool IIdentity.IsAuthenticated => true;

    [NotMapped] string IIdentity.Name => Username;
}
