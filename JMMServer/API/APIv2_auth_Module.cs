using JMMServer.API.Model;
using Nancy;
using Nancy.ModelBinding;

namespace JMMServer.API
{
    public class APIv2_auth_Module : NancyModule
    {
        /// <summary>
        /// Authentication module
        /// </summary>
        public APIv2_auth_Module() : base("/api/auth")
        {
            // Request Body (safer) { "user":"usrname", "pass":"password", "device":"device name" }
            // return apikey=yzx
            Post["/"] = x =>
            {
                string apiKey = "";

                //Bind POST body
                AuthUser auth = this.Bind();
                if (!string.IsNullOrEmpty(auth.user))
                {
                    if (!string.IsNullOrEmpty(auth.device) & !string.IsNullOrEmpty(auth.pass))
                    {
                        //create and save new token for authenticated user or return known one
                        apiKey = UserDatabase.ValidateUser(auth.user, Digest.Hash(auth.pass), auth.device);

                        if (string.IsNullOrEmpty(apiKey))
                        {
                            return new Response { StatusCode = HttpStatusCode.Unauthorized };
                        }
                        else
                        {
                            return this.Response.AsJson(new { apikey = apiKey });
                        }
                    }
                    else
                    {
                        //if password or device is missing
                        return new Response { StatusCode = HttpStatusCode.BadRequest };
                    }
                }
                else
                {
                    //if bind failed
                    return new Response { StatusCode = HttpStatusCode.ExpectationFailed };
                }
            };

            //remove apikey from database
            //pass it as ?apikey=xyz
            Delete["/"] = x =>
            {
                var apiKey = (string)this.Request.Query.apikey;
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

    /// <summary>
    /// Static class so AuthRepo dont have to be recreate
    /// </summary>
    public static class Auth
    {
        public static Repositories.AuthTokensRepository authRepo = new Repositories.AuthTokensRepository();
    }
    
}
