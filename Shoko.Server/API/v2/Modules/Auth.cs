using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Modules
{
    public class Auth : NancyModule
    {
        public static int version = 1;

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
                if (!string.IsNullOrEmpty(auth.user))
                {
                    if (auth.pass == null) auth.pass = string.Empty;
                    if (!string.IsNullOrEmpty(auth.device) && auth.pass != null)
                    {
                        //create and save new token for authenticated user or return known one
                        string apiKey = RepoFactory.AuthTokens.ValidateUser(auth.user, auth.pass, auth.device);

                        if (!string.IsNullOrEmpty(apiKey)) return Response.AsJson(new {apikey = apiKey});

                        return new Response { StatusCode = HttpStatusCode.Unauthorized };
                    }
                    //if password or device is missing
                    return new Response { StatusCode = HttpStatusCode.BadRequest };
                }
                return new Response { StatusCode = HttpStatusCode.ExpectationFailed };
            }, ct);

            //remove apikey from database
            //pass it as ?apikey=xyz
            Delete["/", true] = async (x,ct) => await Task.Factory.StartNew(() =>
            {
                var apiKey = (string) Request.Query.apikey;
                RepoFactory.AuthTokens.DeleteWithToken(apiKey);
                return APIStatus.statusOK();
            }, ct);
        }
    }
}