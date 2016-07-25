using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using JMMServer.Repositories;
using JMMServer.Entities;

namespace JMMServer.API
{
    /// <summary>
    /// UserDatabase is class that help with auth user
    /// </summary>
    public class UserDatabase : IUserMapper
    {
        private static List<Tuple<string, string, Guid>> users = new List<Tuple<string, string, Guid>>();

        static UserDatabase()
        {
            JMMUserRepository repUsers = new JMMUserRepository();
            foreach (JMMUser us in repUsers.GetAll())
            {
                users.Add(new Tuple<string, string, Guid>(us.Username, us.Password, Guid.NewGuid()));
            }
        }

        public IUserIdentity GetUserFromIdentifier(Guid identifier, NancyContext context)
        {
            var userRecord = users.Where(u => u.Item3 == identifier).FirstOrDefault();

            return userRecord == null
                       ? null
                       : new JMMServer.Entities.JMMUser { Username = userRecord.Item1 };
        }

        public static Guid? ValidateUser(string username, string password)
        {
            var userRecord = users.Where(u => u.Item1.ToLower() == username.ToLower() && u.Item2 == password).FirstOrDefault();

            if (userRecord == null)
            {
                return null;
            }

            return userRecord.Item3;
        }
    }
}
