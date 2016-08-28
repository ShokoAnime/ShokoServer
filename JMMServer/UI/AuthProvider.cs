using System.Threading.Tasks;
using System.Windows;

using NutzCode.CloudFileSystem.OAuth2;

namespace JMMServer.UI
{
    public class AuthProvider : IOAuthProvider
    {
        public string Name => "WPF";
        private Window _owner;

        public async Task<AuthResult> Login(AuthRequest request)
        {
            AuthResult r = new AuthResult();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LoginForm l = new LoginForm(request.Name, request.LoginUrl, request.ClientId, request.Scopes, request.RedirectUri,request.ScopesCommaSeparated);
                l.Owner = _owner;
                bool? res = l.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    r.Code = l.Code;
                    r.Scopes = l.Scopes;
                    r.HasError = false;
                }
                else
                {
                    r.HasError = true;
                    r.ErrorString = "Unable to login";
                }

            });
            return r;
        }

        public AuthProvider(Window owner)
        {
            _owner = owner;
        }
    }
}
