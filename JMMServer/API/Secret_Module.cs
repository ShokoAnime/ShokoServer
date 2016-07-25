using Nancy;
using Nancy.Security;
using System.Dynamic;

namespace JMMServer.API
{
    public class Secret_Module : NancyModule
    {
        public Secret_Module() : base("/secure")
        {
            this.RequiresAuthentication();

            Get["/"] = x => {
                dynamic Model = new ExpandoObject();
                Model = new JMMServer.Entities.JMMUser(this.Context.CurrentUser.UserName);
                return View["API/Views/secure", Model];
            };
        }
    }
}
