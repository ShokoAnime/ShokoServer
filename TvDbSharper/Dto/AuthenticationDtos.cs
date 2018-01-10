namespace TvDbSharper.Dto
{
    using System;

    /// <summary>
    /// Represents the data required for authentication
    /// </summary>
    public class AuthenticationData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:TvDbSharper.Dto.AuthenticationData" /> class.
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        /// <param name="username">The Username needed for authentication.</param>
        /// <param name="userKey">The UserKey or Account Identifier found in the account page of your thetvdb.com profile</param>
        public AuthenticationData(string apiKey, string username, string userKey)
        {
            if (apiKey == null)
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("The ApiKey cannot be an empty string or white space.");
            }

            if (username == null)
            {
                throw new ArgumentNullException(nameof(username));
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("The Username cannot be an empty string or white space.");
            }

            if (userKey == null)
            {
                throw new ArgumentNullException(nameof(userKey));
            }

            if (string.IsNullOrWhiteSpace(userKey))
            {
                throw new ArgumentException("The UserKey cannot be an empty string or white space.");
            }

            this.ApiKey = apiKey;
            this.Username = username;
            this.UserKey = userKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:TvDbSharper.Dto.AuthenticationData" /> class.
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        public AuthenticationData(string apiKey)
        {
            if (apiKey == null)
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("The ApiKey cannot be an empty string or white space.");
            }

            this.ApiKey = apiKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:TvDbSharper.Dto.AuthenticationData" /> class.
        /// </summary>
        public AuthenticationData()
        {
        }

        /// <summary>
        /// The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The UserKey or Account Identifier found in the account page of your thetvdb.com profile
        /// </summary>
        public string UserKey { get; set; }

        /// <summary>
        /// The Username needed for authentication.
        /// </summary>
        public string Username { get; set; }
    }

    public class AuthenticationResponse
    {
        public AuthenticationResponse()
        {
        }

        public AuthenticationResponse(string token)
        {
            this.Token = token;
        }

        public string Token { get; set; }
    }
}