using Microsoft.AspNetCore.Authentication;

namespace Shoko.Server.API.Authentication
{
    public class CustomAuthOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "ShokoServer";
        public string Scheme => DefaultScheme;
    }
}
