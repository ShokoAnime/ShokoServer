using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;

namespace Shoko.Server.Settings;

/// <summary>
/// Optional OpenID Connect single sign-on. Disabled by default and fully
/// independent of local username/password login — enabling this adds an
/// additional sign-in option on top of local accounts, it never replaces
/// them. SSO only ever links to a pre-existing local account matched by
/// username/email, unless <see cref="AutoCreateUsers"/> is enabled.
/// </summary>
public class OidcSettings
{
    /// <summary>
    /// Enable the "Sign in with SSO" option on the login page.
    /// </summary>
    [Display(Name = "Enable OIDC Sign-In")]
    [DefaultValue(false)]
    public bool Enabled { get; set; }

    /// <summary>
    /// Label shown on the WebUI's SSO login button, e.g. "Sign in with Authentik".
    /// </summary>
    [Display(Name = "Display Name")]
    [DefaultValue("SSO")]
    public string DisplayName { get; set; } = "SSO";

    /// <summary>
    /// The OIDC provider's issuer URL, e.g. https://auth.example.com/application/o/shoko/.
    /// Shoko fetches /.well-known/openid-configuration from this URL.
    /// </summary>
    [Display(Name = "Issuer/Authority URL")]
    public string? Authority { get; set; }

    /// <summary>
    /// The OAuth2 client ID registered with the OIDC provider for Shoko.
    /// </summary>
    [Display(Name = "Client ID")]
    public string? ClientID { get; set; }

    /// <summary>
    /// The OAuth2 client secret registered with the OIDC provider for Shoko.
    /// </summary>
    [Display(Name = "Client Secret")]
    [DataType(DataType.Password)]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Automatically create a new local account for a subject that signs in
    /// without having been explicitly linked first, using the subject claim
    /// as the username and a randomly generated password. Disabled by
    /// default — sign-in normally only succeeds for an already-linked
    /// account.
    /// </summary>
    [Display(Name = "Auto-Create Users")]
    [DefaultValue(false)]
    public bool AutoCreateUsers { get; set; }
}
