using JMMServer.API.Model;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
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
                    if (!string.IsNullOrEmpty(auth.device) & auth.pass != null)
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

    
}
