using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Shoko.Server.Utilities;

public static class INotifyPropertyChangedExtensions
{
    private static void OnPropertyChanged(this INotifyPropertyChangedExt cls, string caller)
    {
        cls.NotifyPropertyChanged(caller);
    }
    public static void OnPropertyChanged(this INotifyPropertyChangedExt cls, params Expression<Func<object>>[] props)
    {
        foreach (var o in props)
        {
            var member = Resolve(o);
            cls.OnPropertyChanged(member.Member.Name);
        }
    }

    public static object GetPropertyValue(this PropertyChangedEventArgs eventArgs, object sender)
    {
        var property = sender.GetType().GetProperty(eventArgs.PropertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || !property.CanRead) return null;
        return property.GetValue(sender);
    }

    public static void SetField<T>(this INotifyPropertyChangedExt cls, Expression<Func<object>> property, T value, [CallerMemberName] string memberName = "")
    {
        var expr = Resolve(property);       
        T original;
        var changed = false;
        switch (expr.Member)
        {
            case FieldInfo info:
                {
                    original = (T) info.GetValue(cls);
                    changed = !EqualityComparer<T>.Default.Equals(original, value);
                    if (changed)
                        info.SetValue(cls, value);
                    break;
                }
            case PropertyInfo info:
                {
                    original = (T) info.GetValue(cls);
                    changed = !EqualityComparer<T>.Default.Equals(original, value);
                    if (changed)
                        info.SetValue(cls, value);
                    break;
                }
        }

        if (!changed) return;

        cls.OnPropertyChanged(memberName);
    }

    private static MemberExpression Resolve(Expression<Func<object>> expr)
    {
        var member = expr.Body as MemberExpression;
        if (member != null) return member;

        if (expr.Body is UnaryExpression ue)
            member = ue.Operand as MemberExpression;
        return member;
    }
}
