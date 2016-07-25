using JMMServer.Entities;
using JMMServer.Repositories;
using Nancy;
using Nancy.Authentication.Token;
using Nancy.Security;
using Nancy.Authentication.Forms;
using System.Dynamic;
using Nancy.Extensions;
using System;

namespace JMMServer.API
{
    public class Auth_Module : NancyModule
    {
        public Auth_Module() : base("/auth")
        {
            Get["/"] = x =>
            {
                return this.Context.GetRedirect("~/auth/login");
            };

            Get["/login"] = x =>
            {
                dynamic Model = new ExpandoObject();
                Model.Errored = this.Request.Query.error.HasValue;

                return View["API/Views/login", Model];
            };

            Post["/login"] = x =>
            {
                var userGuid = UserDatabase.ValidateUser((string)this.Request.Form.Username, (string)this.Request.Form.Password);

                if (userGuid == null)
                {
                    return this.Context.GetRedirect("~/auth/login?error=true&username=" + (string)this.Request.Form.Username);
                }

                DateTime? expiry = null;
                if (this.Request.Form.RememberMe.HasValue)
                {
                    expiry = DateTime.Now.AddDays(7);
                }

                return this.LoginAndRedirect(userGuid.Value, expiry);
            };

            Get["/logout"] = x =>
            {
                return this.LogoutAndRedirect("~/auth/");
            };
        }
    }
}
