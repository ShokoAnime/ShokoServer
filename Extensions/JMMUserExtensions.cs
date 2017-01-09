using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class JMMUserExtensions
    {

        //TODO Move this to a cache Dictionary when time, memory consumption should be low but, who knows.
        private static Dictionary<string, HashSet<string>> _hidecategoriescache=new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _plexuserscache = new Dictionary<string, HashSet<string>>();

        public static HashSet<string> GetHideCategories(this JMMUser user)
        {
            if (!string.IsNullOrEmpty(user.HideCategories))
            {
                lock (_hidecategoriescache)
                {
                    if (!_hidecategoriescache.ContainsKey(user.HideCategories))
                        _hidecategoriescache[user.HideCategories] = new HashSet<string>(
                            user.HideCategories.Trim().Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _hidecategoriescache[user.HideCategories];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static HashSet<string> GetPlexUsers(this JMMUser user)
        {
            if (!string.IsNullOrEmpty(user.PlexUsers))
            {
                lock (_plexuserscache)
                {
                    if (!_plexuserscache.ContainsKey(user.PlexUsers))
                        _plexuserscache[user.PlexUsers] = new HashSet<string>(
                            user.PlexUsers.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _plexuserscache[user.PlexUsers];
                }
            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
