using System;
using System.Threading.Tasks;
using NutzCode.CloudFileSystem.OAuth2;

namespace Shoko.Server.UI
{
    class AuthProvider : IOAuthProvider
    {
        public string Name => "xxx";

        public event OAuthEventHandler OAuthRequest;

        public delegate Task<AuthResult> OAuthEventHandler(OAuthEventArgs args);

        public Task<AuthResult> LoginAsync(AuthRequest request)
        {
            return OAuthRequest?.Invoke(new OAuthEventArgs(request));
        }
    }

    public class OAuthEventArgs : EventArgs
    {
        public OAuthEventArgs(AuthRequest request)
        {
            Request = request;
        }

        public AuthRequest Request { get; }
    }
}
