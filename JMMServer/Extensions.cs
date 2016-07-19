using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JMMServer
{
    public static class Extensions
    {
        public static void ShallowCopyTo(this object s, object d)
        {
            foreach (PropertyInfo pis in s.GetType().GetProperties())
            {
                foreach (PropertyInfo pid in d.GetType().GetProperties())
                {
                    if (pid.Name == pis.Name)
                        pid.GetSetMethod().Invoke(d, new[] {pis.GetGetMethod().Invoke(s, null)});
                 }
            }
            ;
        }

        public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
        {
            HashSet<string> listhash = list as HashSet<string> ?? new HashSet<string>(list, StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> itemhash = items as HashSet<string> ?? new HashSet<string>(items, StringComparer.InvariantCultureIgnoreCase);
            return listhash.Overlaps(itemhash);
        }

        public static bool FindIn(this string item, IEnumerable<string> list)
        {
            return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}