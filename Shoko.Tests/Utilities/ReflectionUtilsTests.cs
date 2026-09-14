using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
/// <see cref="ReflectionUtils.ScannableAssemblies"/>, which every type scan in the server runs over.
/// </summary>
/// <remarks>
/// Thirteen of them look up job types, subtitle providers, filter expressions and mapped entities this
/// way. <see cref="Assembly.GetTypes"/> throws <see cref="ReflectionTypeLoadException"/> on an
/// assembly still being written to, which surfaced as two unrelated CI failures before the scan
/// learned to skip them.
/// </remarks>
public class ReflectionUtilsTests
{
    [Fact]
    public void TheServerAssemblyIsScanned()
        => Assert.Contains(typeof(ReflectionUtils).Assembly, ReflectionUtils.ScannableAssemblies());

    [Fact]
    public void RuntimeEmittedAssembliesAreNotScanned()
    {
        GC.KeepAlive(new Mock<IDisposable>().Object);

        Assert.DoesNotContain(ReflectionUtils.ScannableAssemblies(), assembly => assembly.IsDynamic);
    }

    [Fact]
    public async Task TheScanSurvivesAssembliesBeingEmittedAlongsideIt()
    {
        // Mocking distinct interfaces makes Castle emit a new proxy type for each rather than serve a
        // cache, which is what a full test run does across its classes.
        var interfaces = typeof(FilterExpression).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic && !type.ContainsGenericParameters)
            .Take(200)
            .ToArray();
        Assert.NotEmpty(interfaces);

        // Ends the loops; deliberately not passed to Task.Run, where a task not yet scheduled when
        // it fires would come back cancelled rather than having run at all.
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
        })).ToArray();

        var scanning = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    _ = ReflectionUtils.ScannableAssemblies().SelectMany(assembly => assembly.GetTypes()).Count();
                }
                catch (Exception exception)
                {
                    failures.Add(exception.GetType().Name);
                }
            }
        })).ToArray();

        await Task.WhenAll(emitting);
        await stop.CancelAsync();
        await Task.WhenAll(scanning);

        Assert.Equal(string.Empty, string.Join(", ", failures.GroupBy(f => f).Select(g => $"{g.Count()}x {g.Key}")));
    }
}
