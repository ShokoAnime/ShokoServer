using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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
/// a null expression, so a failure here silently blanks a saved filter rather than reporting.
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

    [Fact]
    public async Task TheScanSurvivesAssembliesBeingEmittedAlongsideIt()
    {
        // Mocking distinct interfaces makes Castle emit a new proxy type for each rather than serve a
        // cache, which is what a full test run does across its classes. Scanning an assembly while it
        // is still being written to throws `ReflectionTypeLoadException`, and the caller reads that as
        // a filter that would not deserialize.
        var interfaces = typeof(FilterExpression).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic && !type.ContainsGenericParameters)
            .Take(200)
            .ToArray();
        Assert.NotEmpty(interfaces);

        var binder = new SimpleNameSerializationBinder(typeof(FilterExpression));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var failures = new ConcurrentBag<string>();

        var emitting = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            foreach (var type in interfaces.Skip(worker))
            {
                if (stop.IsCancellationRequested)
                    return;

                try
                {
                    GC.KeepAlive(((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type))!).Object);
                }
                catch
                {
                    // Not every interface can be proxied, and only the emitting matters here.
                }
            }
        }, stop.Token)).ToArray();

        var scanning = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    if (binder.BindToType(null, typeof(AndExpression).FullName!) is null)
                        failures.Add("resolved to null");
                }
                catch (Exception exception)
                {
                    failures.Add(exception.GetType().Name);
                }
            }
        }, stop.Token)).ToArray();

        await Task.WhenAll(emitting);
        await stop.CancelAsync();
        await Task.WhenAll(scanning);

        Assert.Equal(string.Empty, string.Join(", ", failures.GroupBy(f => f).Select(g => $"{g.Count()}x {g.Key}")));
    }
}
