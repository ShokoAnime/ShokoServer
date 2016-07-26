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
            //you pass those value as ?user=xxx&pass=xxx
            //return apikey=yzx
            Post["/"] = x =>
            {
                var apiKey = UserDatabase.ValidateUser(
                    (string)this.Request.Query.user,
                    (string)this.Request.Query.pass);

                return string.IsNullOrEmpty(apiKey)
                    ? new Response { StatusCode = HttpStatusCode.Unauthorized }
                    : this.Response.AsJson(new { apikey = apiKey });
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
