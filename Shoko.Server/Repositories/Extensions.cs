using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shoko.Server.Repositories
{
    public static class Extensions
    {
        public const string LocalGenName = "LocalGen";
        public static PropertyBuilder<T> SetLocalValueGenerated<T>(this PropertyBuilder<T> pbuilder)
        {
            //NOTE: We can only do this, because server is the only one updating the database in case of int primary keys.
            //If sometime in the future there is multiple programs updating the database, we have to convert the database primaries from int to GUID.
            return pbuilder.HasAnnotation(LocalGenName, true);
        }

        public static bool NeedsLocalValueGenerated(this IProperty prop)
        {
            return Convert.ToBoolean(prop.FindAnnotation(LocalGenName)?.Value ?? false);
        }
        public static void SetLocalKey<T>(this DbContext context, T nw, Func<PropertyInfo,int> intGenerator)
        {
            List<IProperty> properties = context.Model.FindEntityType(typeof(T)).GetProperties()
                .Where(a => a.NeedsLocalValueGenerated()).ToList();
            foreach (IProperty p in properties)
            {

                PropertyInfo prop = typeof(T).GetProperty(p.Name);
                if (prop != null)
                {
                    if (p.ClrType == typeof(int))
                    {
                        prop.SetValue(nw, intGenerator(prop));
                    }
                    else if (p.ClrType == typeof(Guid))
                    {
                        prop.SetValue(nw, Guid.NewGuid());
                    }
                }
            }
        }
        public static List<PropertyInfo> GetPrimaries<T>(this DbContext context)
        {
            Type t = typeof(T);
            return context.Model.FindEntityType(t).GetProperties().Where(a => a.IsPrimaryKey())
                .Select(a => t.GetProperty(a.Name)).ToList();
        }

        public static string GetName<T>(this DbSet<T> table) where T: class
        {
            return typeof(T).Name.Replace("SVR_",string.Empty);
        }
    }
}

