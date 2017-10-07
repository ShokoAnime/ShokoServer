using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Modules
{
    public class Auth : NancyModule
    {
        /// <summary>
        /// Authentication module
        /// </summary>
        public Auth() : base("/api/auth")
        {
            // Request Body (safer) { "user":"usrname", "pass":"password", "device":"device name" }
            // return apikey=yzx
            Post["/", true] = async (x, ct) => await Task.Factory.StartNew(() =>
            {
                //Bind POST body
                AuthUser auth = this.Bind();
                if (string.IsNullOrEmpty(auth.user?.Trim()))
                    return new Response {StatusCode = HttpStatusCode.BadRequest};

                if (auth.pass == null) auth.pass = string.Empty;
                //if password or device is missing
                if (string.IsNullOrEmpty(auth.device) || auth.pass == null)
                    return new Response {StatusCode = HttpStatusCode.BadRequest};

                //create and save new token for authenticated user or return known one
                string apiKey = RepoFactory.AuthTokens.ValidateUser(auth.user.Trim(), auth.pass.Trim(), auth.device.Trim());

                if (!string.IsNullOrEmpty(apiKey)) return Response.AsJson(new {apikey = apiKey});

                return new Response { StatusCode = HttpStatusCode.Unauthorized };
            }, ct);

            //remove apikey from database
            //pass it as ?apikey=xyz
            Delete["/", true] = async (x,ct) => await Task.Factory.StartNew(() =>
            {
                var apiKey = (string) Request.Query.apikey;
                RepoFactory.AuthTokens.DeleteWithToken(apiKey);
                return APIStatus.OK();
            }, ct);
        }
    }
}