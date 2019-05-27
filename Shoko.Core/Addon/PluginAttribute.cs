namespace Shoko.Core.Addon
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class PluginAttribute : System.Attribute
    {
        // See the attribute guidelines at
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly string pluginID;

        // This is a positional argument
        public PluginAttribute(string PluginID)
        {
            this.pluginID = PluginID;
        }

        public string PluginID
        {
            get { return pluginID; }
        }
    }
}