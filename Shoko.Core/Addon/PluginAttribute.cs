namespace Shoko.Core.Addon
{
    /// <summary>
    /// This is required to exist on all plugins, they must also inherit the interface of <see cref="IPlugin"/> <br/>
    /// You might also want to look at inheriting <see cref="ISignalRPlugin"/> if you want to add SignalR functionality to the webserver.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PluginAttribute : System.Attribute
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