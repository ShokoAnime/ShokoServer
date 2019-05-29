using Microsoft.IdentityModel.Tokens;
using Shoko.Core.API.Models;
using Shoko.Core.Database;
using Shoko.Core.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace Shoko.Core.API.Services
{
    public interface IUserService
    {
        OAuthResponse Authenticate(string username, string password);
        //IEnumerable<User> GetAll();
    }

    internal class UserService : IUserService
    {
        private readonly CoreDbContext _db;

        public UserService(CoreDbContext db)
        {
            _db = db;
        }

        public OAuthResponse Authenticate(string username, string password)
        {
            //SHA256 if there is any contents.
            string hashedPassword = string.IsNullOrEmpty(password) ?
                string.Empty :
                BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "");

            var user = _db.Users.SingleOrDefault(x => 
                    string.Equals(x.Username, username, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(x.Password ?? "", hashedPassword, StringComparison.InvariantCultureIgnoreCase)
                );

            if (user == null) return null;

            var scopes = new List<string>();
            scopes.Add("user");
            if (user.IsAdmin) scopes.Add("admin");

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim("scope", string.Join(" ", scopes))
                }),
                Expires = DateTime.UtcNow + ShokoServer.JwtLifespan, //Token is valid for a year.
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Config.ConfigurationLoader.CoreConfig.JwtSecret)), SecurityAlgorithms.HmacSha256)
            };
            user.Token = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            _db.Users.Update(user);
            _db.SaveChanges();

            return new OAuthResponse()
            {
                AccessToken = user.Token,
                Scope = string.Join(" ", scopes),
                ExpiresIn = ShokoServer.JwtLifespan.TotalSeconds
            };
        }
    }
}
