using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Shoko.Server.Plugin;

/// <summary>
///   Finds what a plugin DLL refers to in the server's own assemblies that
///   the running server no longer has, by reading the DLL's metadata without
///   loading it.
/// </summary>
/// <remarks>
///   A plugin built for an older contract loads fine and only throws once
///   the missing type or member is touched, which is anywhere from the
///   plugin scan to a request weeks later. Reading its references up front
///   names the plugin and what it misses before any of its code runs.
/// </remarks>
internal static class PluginReferenceChecker
{
    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>
    ///   Lists the types, methods and fields the DLL refers to in the given
    ///   assemblies that those assemblies do not have. Methods are matched
    ///   by name and parameter count, fields by name. References through a
    ///   constructed generic type or an array are not checked.
    /// </summary>
    /// <param name="dllPath">The DLL to read.</param>
    /// <param name="hostAssemblies">The server's assemblies to check against, by simple name.</param>
    /// <returns>One description per missing reference, or none for a DLL without managed metadata.</returns>
    /// <exception cref="IOException">The DLL cannot be read.</exception>
    /// <exception cref="BadImageFormatException">The DLL is not a valid image.</exception>
    public static IReadOnlyList<string> FindMissingReferences(string dllPath, IReadOnlyDictionary<string, Assembly> hostAssemblies)
    {
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return [];

        var reader = peReader.GetMetadataReader();
        var resolved = new Dictionary<TypeReferenceHandle, HostType?>();
        var missing = new List<string>();
        foreach (var handle in reader.TypeReferences)
            ResolveType(reader, handle, hostAssemblies, resolved, missing);

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind is not HandleKind.TypeReference)
                continue;

            if (resolved.GetValueOrDefault((TypeReferenceHandle)member.Parent) is not { Type: { } type } hostType)
                continue;

            var name = reader.GetString(member.Name);
            try
            {
                var signature = reader.GetBlobReader(member.Signature);
                var header = signature.ReadSignatureHeader();
                if (header.Kind is SignatureKind.Field)
                {
                    if (GetCandidateTypes(type).All(candidate => candidate.GetField(name, AllMembers) is null))
                        missing.Add($"field {hostType.FullName}.{name}");
                }
                else if (header.Kind is SignatureKind.Method)
                {
                    var genericParameterCount = header.IsGeneric ? signature.ReadCompressedInteger() : 0;
                    var parameterCount = signature.ReadCompressedInteger();
                    if (!HasMethod(type, name, parameterCount, genericParameterCount))
                        missing.Add($"method {hostType.FullName}.{name} with {parameterCount} parameter{(parameterCount is 1 ? "" : "s")}");
                }
            }
            // A member the runtime cannot even describe is not one this check can speak for.
            catch (Exception ex) when (ex is BadImageFormatException or TypeLoadException or FileNotFoundException or FileLoadException)
            {
            }
        }

        return missing;
    }

    private sealed record HostType(Assembly Assembly, string FullName, Type? Type);

    private static HostType? ResolveType(
        MetadataReader reader,
        TypeReferenceHandle handle,
        IReadOnlyDictionary<string, Assembly> hostAssemblies,
        Dictionary<TypeReferenceHandle, HostType?> resolved,
        List<string> missing
    )
    {
        if (resolved.TryGetValue(handle, out var known))
            return known;

        var reference = reader.GetTypeReference(handle);
        var name = reader.GetString(reference.Name);
        HostType? result = null;
        switch (reference.ResolutionScope.Kind)
        {
            case HandleKind.AssemblyReference:
            {
                var assemblyName = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
                if (!hostAssemblies.TryGetValue(assemblyName, out var assembly))
                    break;

                var typeNamespace = reader.GetString(reference.Namespace);
                var fullName = typeNamespace.Length > 0 ? $"{typeNamespace}.{name}" : name;
                result = new(assembly, fullName, assembly.GetType(fullName, throwOnError: false));
                if (result.Type is null)
                    missing.Add($"type {fullName}");
                break;
            }
            case HandleKind.TypeReference:
            {
                if (ResolveType(reader, (TypeReferenceHandle)reference.ResolutionScope, hostAssemblies, resolved, missing) is not { } parent)
                    break;

                var fullName = $"{parent.FullName}+{name}";
                result = new(parent.Assembly, fullName, parent.Type?.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic));
                // A missing outer type was already listed, and takes its nested types with it.
                if (result.Type is null && parent.Type is not null)
                    missing.Add($"type {fullName}");
                break;
            }
        }

        resolved[handle] = result;
        return result;
    }

    /// <summary>
    ///   The type itself, and for an interface the interfaces it extends, as
    ///   reflection does not list their members on the interface.
    /// </summary>
    private static IEnumerable<Type> GetCandidateTypes(Type type)
        => type.IsInterface ? type.GetInterfaces().Prepend(type) : [type];

    private static bool HasMethod(Type type, string name, int parameterCount, int genericParameterCount)
    {
        if (name is ".ctor")
            return type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(constructor => constructor.GetParameters().Length == parameterCount);

        return GetCandidateTypes(type)
            .SelectMany(candidate => candidate.GetMethods(AllMembers))
            .Any(method => method.Name == name &&
                method.GetParameters().Length == parameterCount &&
                (method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0) == genericParameterCount);
    }
}
