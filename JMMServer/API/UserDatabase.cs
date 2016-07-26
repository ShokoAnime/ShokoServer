using System;
using System.Collections.Generic;
using System.Linq;

namespace JMMServer.API
{
    /// <summary>
    /// UserDatabase is class that help with auth user
    /// </summary>
    public class UserDatabase
    {
        static readonly List<Tuple<string, string>> ActiveApiKeys = new List<Tuple<string, string>>();
        private static readonly List<Tuple<string, string>> Users = new List<Tuple<string, string>>();

        static UserDatabase()
        {
            JMMServer.Repositories.JMMUserRepository repUsers = new JMMServer.Repositories.JMMUserRepository();
            foreach (JMMServer.Entities.JMMUser us in repUsers.GetAll())
            {
                Users.Add(new Tuple<string, string>(us.Username, us.Password));
            }
        }

        public static JMMServer.Entities.JMMUser GetUserFromApiKey(string apiKey)
        {
            var activeKey = ActiveApiKeys.FirstOrDefault(x => x.Item2 == apiKey);

            if (activeKey == null)
            {
                return null;
            }

            var userRecord = Users.First(u => u.Item1 == activeKey.Item1);
            return new JMMServer.Entities.JMMUser(userRecord.Item1);
        }

        public static string ValidateUser(string username, string password)
        {
            var userRecord = Users.FirstOrDefault(u => u.Item1 == username && u.Item2 == password);

            if (userRecord == null)
            {
                return null;
            }
                        
            var apiKey = Guid.NewGuid().ToString();
            ActiveApiKeys.Add(new Tuple<string, string>(username, apiKey));
            return apiKey;
        }

        public static void RemoveApiKey(string apiKey)
        {
            var apiKeyToRemove = ActiveApiKeys.First(x => x.Item2 == apiKey);
            ActiveApiKeys.Remove(apiKeyToRemove);
        }

        public static Tuple<string, string> CreateUser(string username, string password)
        {
            var user = new Tuple<string, string>(username, password);
            Users.Add(user);
            return user;
        }
    }
}
