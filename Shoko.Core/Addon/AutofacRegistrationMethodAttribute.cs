namespace Shoko.Core.Addon
{
    /// <summary>
    /// This signifies that this is to be called for the Autofac builder.<br/>
    /// Needs to have a single paramater of <see cref="Autofac.ContainerBuilder"/> also needs to be a <see langword="static"/> method.<br/>
    /// See <a href="https://autofaccn.readthedocs.io/en/latest/register/registration.html">Here</a> for some more information
    /// </summary>
    /// <example>
    /// This is an example AutoFac registration method
    /// <code>
    /// public static RegisterAutofac(Autofac.ContainerBuilder builder)
    /// </code>
    /// </example>
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AutofacRegistrationMethodAttribute : System.Attribute
    {
        public AutofacRegistrationMethodAttribute()
        {
           
        }
    }
}