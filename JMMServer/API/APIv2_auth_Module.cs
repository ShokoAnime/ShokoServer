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
            //you pass those value as ?user=xxx&device=yyy&pass=zzz
            // or 
            // as Request Body (safer) { "user":"usrname", "pass":"password", "device":"device name" }
            //return apikey=yzx
            Post["/"] = x =>
            {
                string apiKey = "";

                //Bind POST body
                AuthUser auth = this.Bind();
                //create new token for authenticated user or return known one
                apiKey = UserDatabase.ValidateUser(auth.user, Digest.Hash(auth.pass), auth.device);

                if (string.IsNullOrEmpty(apiKey))
                {
                    return new Response { StatusCode = HttpStatusCode.Unauthorized };
                }
                else
                {
                    //get user knowing his username
                    try
                    {
                        int uid = RepoFactory.JMMUser.GetByUsername(auth.user).JMMUserID;
                        Entities.AuthTokens token = new Entities.AuthTokens(uid, (auth.device).ToLower(), apiKey);
                        //save token for auth user
                        RepoFactory.AuthTokens.Save(token);
                    }
                    catch
                    {
                        apiKey = "";
                    }

                    return this.Response.AsJson(new { apikey = apiKey });
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
