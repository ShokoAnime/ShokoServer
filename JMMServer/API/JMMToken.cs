namespace JMMServer.API
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using JWT;
    using System.Security.Claims;
    using Owin.StatelessAuth;

    public class JMMToken : ITokenValidator
    {
        private readonly IConfigProvider configProvider;

        public JMMToken(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
        }

        public ClaimsPrincipal ValidateUser(string token)
        {
            try
            {
                var decodedtoken = JsonWebToken.DecodeToObject(token, configProvider.GetAppSetting("securekey")) as Dictionary<string, object>;

                var jwttoken = new JwtToken()
                {
                    Audience = (string)decodedtoken["Audience"],
                    Issuer = (string)decodedtoken["Issuer"],
                    Expiry = DateTime.Parse(decodedtoken["Expiry"].ToString()),
                };

                if (decodedtoken.ContainsKey("Claims"))
                {
                    var claims = new List<Claim>();

                    for (int i = 0; i < ((ArrayList)decodedtoken["Claims"]).Count; i++)
                    {
                        var type = ((Dictionary<string, object>)((ArrayList)decodedtoken["Claims"])[i])["Type"].ToString();
                        var value = ((Dictionary<string, object>)((ArrayList)decodedtoken["Claims"])[i])["Value"].ToString();
                        claims.Add(new Claim(type, value));
                    }

                    jwttoken.Claims = claims;
                }

                if (jwttoken.Expiry < DateTime.UtcNow)
                {
                    return null;
                }

                var claimsPrincipal = new ClaimsPrincipal();
                var claimsIdentity = new ClaimsIdentity("Token");
                claimsIdentity.AddClaims(jwttoken.Claims);
                claimsPrincipal.AddIdentity(claimsIdentity);
                return claimsPrincipal;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public class JwtToken
        {
            public string Issuer { get; set; }
            public string Audience { get; set; }
            public IEnumerable<Claim> Claims { get; set; }
            public DateTime Expiry { get; set; }
        }

        public interface IConfigProvider
        {
            string GetAppSetting(string propertyName);
            T GetAppSetting<T>(string propertyName) where T : struct;
        }

        public class JwtWrapper : IJwtWrapper
        {
            public string Encode(object payload, string key, JwtHashAlgorithm algorithm)
            {
                return JsonWebToken.Encode(payload, key, algorithm);
            }
        }

        public interface IJwtWrapper
        {
            string Encode(object payload, string key, JwtHashAlgorithm algorithm);
        }
    }
}
