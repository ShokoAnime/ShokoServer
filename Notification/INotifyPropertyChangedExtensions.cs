using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
namespace Shoko.Commons.Notification
{
    public static class INotifyPropertyChangedExtensions
    {
        public static void OnPropertyChanged(this INotifyPropertyChangedExt cls, Expression<Func<object>> expr)
        {
            MemberExpression me = Resolve(expr);
            if (me != null) cls.NotifyPropertyChanged(me.Member.Name);
        }
        public static void OnPropertyChanged(this INotifyPropertyChangedExt cls, params Expression<Func<object>>[] props)
        {
            foreach (Expression<Func<object>> o in props)
            {
                cls.OnPropertyChanged(o);
            }

        }
        /*
        public static T SetField<T>(this INotifyPropertyChangedExt cls, T field, T value, params Expression<Func<object>>[] props)
        {
            //if (!EqualityComparer<T>.Default.Equals(field, value))
            //{
                cls.OnPropertyChanged(props);
            //}
            return value;
        }
        public static T SetField<T>(this INotifyPropertyChangedExt cls, T field, T value, [CallerMemberName] string propertyName = null)
        {
            //if (!EqualityComparer<T>.Default.Equals(field, value))
            //{
                cls.NotifyPropertyChanged(propertyName);
            //}
            return value;
        }
        */
        public static void SetField<T>(this INotifyPropertyChangedExt cls, Expression<Func<object>> property, T value, params Expression<Func<object>>[] props)
        {
            var expr = Resolve(property);       
            T original;
            bool changed = false;
            if (expr.Member is FieldInfo)
            {
                original = (T) ((FieldInfo) expr.Member).GetValue(cls);
                changed = !EqualityComparer<T>.Default.Equals(original, value);
                if (changed)
                    ((FieldInfo) expr.Member).SetValue(cls, value);
            }
            else if (expr.Member is PropertyInfo)
            {
                original = (T) ((PropertyInfo) expr.Member).GetValue(cls);
                changed = !EqualityComparer<T>.Default.Equals(original, value);
                if (changed)
                    ((PropertyInfo) expr.Member).SetValue(cls, value);
            }
            if (changed)
            {
                cls.OnPropertyChanged(property);
                if (props != null && props.Length > 0)
                    cls.OnPropertyChanged(props);
            }
        }

        private static MemberExpression Resolve(Expression<Func<object>> expr)
        {
            var member = expr.Body as MemberExpression;
            if (member == null)
            {
                var ue = expr.Body as UnaryExpression;
                if (ue != null)
                    member = ue.Operand as MemberExpression;
            }
            return member;
        }
        public static void SetField<T>(this INotifyPropertyChangedExt cls, Func<T> getter, Action<T> setter, T value, params Expression<Func<object>>[] props)
        {
            T original = getter();
            bool changed = false;
            changed = !EqualityComparer<T>.Default.Equals(original, value);
            if (changed)
                setter(value);
            if (changed)
            {
                if (props != null && props.Length > 0)
                    cls.OnPropertyChanged(props);
            }
        }
    }
}
