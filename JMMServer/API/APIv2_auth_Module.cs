﻿using JMMServer.API.Model;
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
                    Repositories.JMMUserRepository userRepo = new Repositories.JMMUserRepository();
                    try
                    {
                        int uid = userRepo.GetByUsername(auth.user).JMMUserID;
                        Entities.AuthTokens token = new Entities.AuthTokens(uid, (auth.device).ToLower(), apiKey);
                        //save token for auth user
                        Auth.authRepo.Save(token);
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

    /// <summary>
    /// Static class so AuthRepo dont have to be recreate
    /// </summary>
    public static class Auth
    {
        public static Repositories.AuthTokensRepository authRepo = new Repositories.AuthTokensRepository();
    }
    
}
