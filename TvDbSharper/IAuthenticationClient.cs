namespace TvDbSharper
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for obtaining and refreshing your JWT token
    /// </summary>
    public interface IAuthenticationClient
    {
        /// <summary>
        /// <para>Gets or sets the authentication token that gets stored after calling <see cref="AuthenticateAsync(AuthenticationData, CancellationToken)"/></para> 
        /// </summary>
        string Token { get; set; }

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="authenticationData">The data required for authentication</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(AuthenticationData authenticationData, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        /// <param name="username">The Username needed for authentication.</param>
        /// <param name="userKey">The UserKey or Account Identifier found in the account page of your thetvdb.com profile</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(string apiKey, string username, string userKey, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        /// <param name="username">The Username needed for authentication.</param>
        /// <param name="userKey">The UserKey or Account Identifier found in the account page of your thetvdb.com profile</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(string apiKey, string username, string userKey);

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(string apiKey, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="apiKey">The ApiKey needed for authentication. Can be generated here: https://thetvdb.com/?tab=apiregister </param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(string apiKey);

        /// <summary>
        /// <para>[POST /login]</para>
        /// <para>Authenticates the user given an authentication data and retrieves a session token.</para> 
        /// <para>The session token is only valid for 24 hours, but the session can be extended by calling <see cref="RefreshTokenAsync()" /></para>
        /// <para>Call once before calling any other method.</para>
        /// </summary>
        /// <param name="authenticationData">The data required for authentication</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task AuthenticateAsync(AuthenticationData authenticationData);

        /// <summary>
        /// <para>[GET /refresh_token]</para>
        /// <para>Returns a new session token that extends the current session by 24 hours.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RefreshTokenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /refresh_token]</para>
        /// <para>Returns a new session token that extends the current session by 24 hours.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RefreshTokenAsync();
    }
}