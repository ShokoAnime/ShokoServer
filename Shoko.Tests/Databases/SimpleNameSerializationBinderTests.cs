using System;
using System.Threading;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// <see cref="SimpleNameSerializationBinder"/>, which resolves a stored filter's type from its short
/// name by scanning every loaded assembly.
/// </summary>
/// <remarks>
/// Anything it throws is swallowed by <c>FilterExpressionConverter</c>'s error handler, which returns
/// a null expression, so a failure here silently blanks a saved filter rather than reporting. The
/// scan it runs over is covered by <see cref="Utilities.ReflectionUtilsTests"/>.
/// </remarks>
public class SimpleNameSerializationBinderTests
{
    [Fact]
    public void ATypeIsFoundByItsShortName()
        => Assert.Equal(typeof(AndExpression),
            new SimpleNameSerializationBinder(typeof(FilterExpression)).BindToType(null, typeof(AndExpression).FullName!));

    [Fact]
    public void ATypeOutsideTheBaseTypeIsNotReturned()
        => Assert.Null(new SimpleNameSerializationBinder(typeof(FilterExpression)).BindToType(null, typeof(string).FullName!));
}
