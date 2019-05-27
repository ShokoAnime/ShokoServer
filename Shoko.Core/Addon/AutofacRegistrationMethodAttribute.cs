namespace Shoko.Core.Addon
{
    /// <summary>
    /// This signifies that this is to be called for the Autofac builder.
    /// Needs to have a signature of 
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AutofacRegistrationMethodAttribute : System.Attribute
    {
        public AutofacRegistrationMethodAttribute()
        {
           
        }
    }
}