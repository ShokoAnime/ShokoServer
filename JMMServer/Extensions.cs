using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
						(pid.GetSetMethod()).Invoke(d, new[] { pis.GetGetMethod().Invoke(s, null) });
				}
			};
		}

		public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
		{
			List<string> cats = items.Select(a => a.ToLowerInvariant()).ToList();
			HashSet<string> ourcats = new HashSet<string>(list.Select(a => a.ToLowerInvariant()));
			foreach (string cat in cats)
			{
				string n = cat.Trim();
				if (!String.IsNullOrEmpty(n) && ourcats.Contains(n))
					return true;
			}
			return false;
		}

		public static bool FindIn(this string item, IEnumerable<string> list)
		{
			HashSet<string> ourcats = new HashSet<string>(list.Select(a => a.ToLowerInvariant()));
			string n = item.ToLowerInvariant().Trim();
			if (!String.IsNullOrEmpty(n) && ourcats.Contains(n))
				return true;
			return false;
		}
	}
}
