using Nancy;

namespace JMMServer.API
{
    public class Auth_Module : NancyModule
    {
        /// <summary>
        /// Authentication module
        /// </summary>
        public Auth_Module() : base("/auth")
        {
            //you pass those value as ?user=xxx&device=yyy$oass=zzz
            //return apikey=yzx
            Post["/"] = x =>
            {
                var apiKey = UserDatabase.ValidateUser(
                    (string)this.Request.Query.user,
                    (string)this.Request.Query.pass,
                    (string)this.Request.Query.device);

                if (string.IsNullOrEmpty(apiKey))
                {
                    return new Response { StatusCode = HttpStatusCode.Unauthorized };
                }
                else
                {
                    //create new token for authenticated user
                    //get user knowing his username
                    Repositories.JMMUserRepository userRepo = new Repositories.JMMUserRepository();
                    int uid = userRepo.GetByUsername((string)this.Request.Query.user).JMMUserID;
                    Entities.AuthTokens token = new Entities.AuthTokens(uid, ((string)this.Request.Query.device).ToLower(), apiKey);
                    //save token for auth user
                    Repositories.AuthTokensRepository authRepo = new Repositories.AuthTokensRepository();
                    authRepo.Save(token);

                    return this.Response.AsJson(new { apikey = apiKey });
                }
            };

            //remove apikey from database
            Delete["/"] = x =>
            {
                var apiKey = (string)this.Request.Query.apikey;
                UserDatabase.RemoveApiKey(apiKey);
                return new Response { StatusCode = HttpStatusCode.OK };
            };
        }
    }
}
