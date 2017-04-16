using Nancy;
using Nancy.ModelBinding;
using Shoko.Server.API.core;
using Shoko.Server.API.v2.Models.core;

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
            Post["/", true] = async (x,ct) =>
            {
                string apiKey = "";

                //Bind POST body
                AuthUser auth = this.Bind();
                if (!string.IsNullOrEmpty(auth.user))
                {
                    if (auth.pass == null) auth.pass = "";
                    if (!string.IsNullOrEmpty(auth.device) && auth.pass != null)
                    {
                        //create and save new token for authenticated user or return known one
                        apiKey = UserDatabase.ValidateUser(auth.user, Digest.Hash(auth.pass), auth.device);

                        if (string.IsNullOrEmpty(apiKey))
                        {
                            return new Response {StatusCode = HttpStatusCode.Unauthorized};
                        }
                        return this.Response.AsJson(new {apikey = apiKey});
                    }
                    //if password or device is missing
                    return new Response {StatusCode = HttpStatusCode.BadRequest};
                }
                return new Response {StatusCode = HttpStatusCode.ExpectationFailed};
            };

            //remove apikey from database
            //pass it as ?apikey=xyz
            Delete["/", true] = async (x,ct) =>
            {
                var apiKey = (string) this.Request.Query.apikey;
                if (UserDatabase.RemoveApiKey(apiKey))
                {
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.InternalServerError;
                }
            };
        }
    }
}