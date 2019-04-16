using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using Shoko.Server.Models;

namespace Shoko.Server.API.Authentication
{
    // This is a fake user to aid in Authentication during first run
    public class InitUser : SVR_JMMUser, IIdentity
    {
        public static InitUser Instance { get; } = new InitUser();

        private InitUser()
        {
            JMMUserID = 0;
            Username = "init";
            Password = "";
            IsAdmin = 1;
            HideCategories = "";
            CanEditServerSettings = 1;
            
        }
        
        [NotMapped] string IIdentity.AuthenticationType => "API";

        [NotMapped] bool IIdentity.IsAuthenticated => true;

        [NotMapped] string IIdentity.Name => Username;
    }
}