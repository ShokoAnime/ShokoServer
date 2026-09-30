using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Moq;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
/// Covers <see cref="ReflectionUtils.ScannableAssemblies"/>, which every type scan in the server runs
/// over. <see cref="Assembly.GetTypes"/> throws <see cref="ReflectionTypeLoadException"/> on an
/// assembly still being written to, so the scan has to skip those.
/// </summary>
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
    public void TheScanSurvivesAssembliesBeingEmittedAlongsideIt()
    {
        // An assembly caught halfway through being written, held in that state for the whole scan
        // rather than raced for: one type baked, one still being built.
        var pending = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"Pending{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
        var module = pending.DefineDynamicModule("Pending");
        module.DefineType("Pending.Baked", TypeAttributes.Public | TypeAttributes.Class).CreateType();
        var building = module.DefineType("Pending.Building", TypeAttributes.Public | TypeAttributes.Class, typeof(FilterExpression));
        var loaded = Assert.Single(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.FullName == pending.FullName);

        // Read the way the server's scans read their types, it throws.
        Assert.ThrowsAny<Exception>(() => Scan([loaded]));

        Assert.DoesNotContain(ReflectionUtils.ScannableAssemblies(), assembly => assembly.FullName == pending.FullName);
        Assert.NotEmpty(Scan(ReflectionUtils.ScannableAssemblies()));

        GC.KeepAlive(building);
    }

    /// <summary>
    /// Reads every type of the assemblies the way the server's scans do: its kind, what it derives
    /// from, and its attributes.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The filter expressions found.</returns>
    private static IReadOnlyList<Type> Scan(IEnumerable<Assembly> assemblies)
        => assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && typeof(FilterExpression).IsAssignableFrom(type) && !type.IsDefined(typeof(ObsoleteAttribute), true))
            .ToList();
}
