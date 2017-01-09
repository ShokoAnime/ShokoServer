using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.WCFCompression
{
    public class PlexKodiFilterInspector : IParameterInspector
    {
        private SerializationFilter _filter;
        public PlexKodiFilterInspector(SerializationFilter filter)
        {
            _filter = filter;
        }
        public object BeforeCall(string operationName, object[] inputs)
        {
            return null;
        }

        public void AfterCall(string operationName, object[] outputs, object returnValue, object correlationState)
        {
            if (_filter==SerializationFilter.Kodi)
               returnValue.NullPropertiesWithAttribute(typeof(Shoko.Models.PlexAndKodi.Plex));
            else if (_filter==SerializationFilter.Plex)
                returnValue.NullPropertiesWithAttribute(typeof(Shoko.Models.PlexAndKodi.Kodi));
        }
    }
    public static class ClassExtensions
    {
        public static bool IsNullable<T>(this T obj)
        {
            if (obj == null) return true; 
            Type type = typeof(T);
            if (!type.IsValueType) return true;
            if (Nullable.GetUnderlyingType(type) != null) return true; 
            return false; 
        }

        public static void NullPropertiesWithAttribute(this object obj, Type attrtype)
        {
            if (obj == null)
                return;
            Type objType = obj.GetType();
            PropertyInfo[] properties = objType.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object propValue = property.GetValue(obj, null);
                IList list = propValue as IList;
                if (list != null)
                {
                    foreach (object item in list)
                    {
                        item.NullPropertiesWithAttribute(attrtype);
                    }
                }
                else
                {
                    if (property.PropertyType.Assembly == objType.Assembly)
                    {
                        propValue.NullPropertiesWithAttribute(attrtype);
                    }
                    else
                    {
                        if (Attribute.IsDefined(property, attrtype))
                        {
                            if (propValue.IsNullable())
                            {
                                property.SetValue(obj,null);
                            }
                        }
                    }
                }
            }
        }
    }
}
