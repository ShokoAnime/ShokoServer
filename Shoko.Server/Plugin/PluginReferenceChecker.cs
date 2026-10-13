using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Shoko.Server.Plugin;

/// <summary>
///   Finds what a plugin DLL refers to in other assemblies, the server's own
///   or those of a plugin it depends on, that those assemblies do not have,
///   by reading metadata on both sides without loading anything.
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
    ///   What an assembly defines, as far as a reference to it can be checked:
    ///   its types, with their methods, fields and base types.
    /// </summary>
    internal sealed class AssemblySurface
    {
        /// <summary>
        ///   The assembly's simple name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        ///   The types the assembly defines, by full name, a nested one as
        ///   <c>Outer+Inner</c>.
        /// </summary>
        public required IReadOnlyDictionary<string, TypeSurface> Types { get; init; }

        /// <summary>
        ///   The full names of the types the assembly forwards elsewhere.
        ///   They exist, but their members cannot be checked here.
        /// </summary>
        public required IReadOnlySet<string> ForwardedTypes { get; init; }

        /// <summary>
        ///   Reads what a DLL defines.
        /// </summary>
        /// <param name="dllPath">The DLL to read.</param>
        /// <returns>Its surface, or <c>null</c> for a file without managed metadata.</returns>
        /// <exception cref="IOException">The DLL cannot be read.</exception>
        /// <exception cref="BadImageFormatException">The DLL is not a valid image.</exception>
        public static AssemblySurface? Read(string dllPath)
        {
            using var stream = File.OpenRead(dllPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return null;

            var reader = peReader.GetMetadataReader();
            if (!reader.IsAssembly)
                return null;

            var types = new Dictionary<string, TypeSurface>();
            foreach (var handle in reader.TypeDefinitions)
            {
                var definition = reader.GetTypeDefinition(handle);
                var methods = new HashSet<(string, int, int)>();
                foreach (var methodHandle in definition.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    var signature = reader.GetBlobReader(method.Signature);
                    var header = signature.ReadSignatureHeader();
                    var genericParameterCount = header.IsGeneric ? signature.ReadCompressedInteger() : 0;
                    methods.Add((reader.GetString(method.Name), signature.ReadCompressedInteger(), genericParameterCount));
                }

                var bases = new List<TypeName?>();
                if (!definition.BaseType.IsNil)
                    bases.Add(GetTypeName(reader, definition.BaseType));
                foreach (var interfaceHandle in definition.GetInterfaceImplementations())
                    bases.Add(GetTypeName(reader, reader.GetInterfaceImplementation(interfaceHandle).Interface));

                types[GetFullName(reader, handle)] = new()
                {
                    Methods = methods,
                    Fields = definition.GetFields().Select(fieldHandle => reader.GetString(reader.GetFieldDefinition(fieldHandle).Name)).ToHashSet(),
                    Bases = bases,
                };
            }

            var forwarded = new HashSet<string>();
            foreach (var handle in reader.ExportedTypes)
            {
                var exported = reader.GetExportedType(handle);
                if (!exported.IsForwarder)
                    continue;

                var typeNamespace = reader.GetString(exported.Namespace);
                var name = reader.GetString(exported.Name);
                forwarded.Add(typeNamespace.Length > 0 ? $"{typeNamespace}.{name}" : name);
            }

            return new()
            {
                Name = reader.GetString(reader.GetAssemblyDefinition().Name),
                Types = types,
                ForwardedTypes = forwarded,
            };
        }
    }

    /// <summary>
    ///   What a type defines by itself.
    /// </summary>
    internal sealed class TypeSurface
    {
        /// <summary>
        ///   The type's own methods, by name, parameter count and generic
        ///   parameter count.
        /// </summary>
        public required IReadOnlySet<(string Name, int ParameterCount, int GenericParameterCount)> Methods { get; init; }

        /// <summary>
        ///   The names of the type's own fields.
        /// </summary>
        public required IReadOnlySet<string> Fields { get; init; }

        /// <summary>
        ///   The type's base type and the interfaces it lists, or
        ///   <c>null</c> for one that could not be named.
        /// </summary>
        public required IReadOnlyList<TypeName?> Bases { get; init; }
    }

    /// <summary>
    ///   A type by the assembly it lives in and its full name.
    /// </summary>
    /// <param name="AssemblyName">The assembly's simple name, or <c>null</c> for the assembly naming it.</param>
    /// <param name="FullName">The type's full name, a nested one as <c>Outer+Inner</c>.</param>
    internal readonly record struct TypeName(string? AssemblyName, string FullName);

    /// <summary>
    ///   Lists the types, methods and fields the DLL refers to in the given
    ///   assemblies that those assemblies do not have. Methods are matched
    ///   by name, parameter count and generic parameter count, fields by
    ///   name, both through the base types and interfaces that can be
    ///   followed. A member is only listed when every place it could be
    ///   inherited from was looked at; references through an array are not
    ///   checked.
    /// </summary>
    /// <param name="dllPath">The DLL to read.</param>
    /// <param name="targets">The assemblies to check against, by simple name.</param>
    /// <returns>One description per missing reference, or none for a DLL without managed metadata.</returns>
    /// <exception cref="IOException">The DLL cannot be read.</exception>
    /// <exception cref="BadImageFormatException">The DLL is not a valid image.</exception>
    public static IReadOnlyList<string> FindMissingReferences(string dllPath, IReadOnlyDictionary<string, AssemblySurface> targets)
    {
        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return [];

        var reader = peReader.GetMetadataReader();
        var missing = new List<string>();
        var missingTypes = new HashSet<TypeName>();
        foreach (var handle in reader.TypeReferences)
        {
            if (GetTypeName(reader, handle) is not { AssemblyName: { } assemblyName } name || !targets.TryGetValue(assemblyName, out var target))
                continue;

            if (target.Types.ContainsKey(name.FullName) || target.ForwardedTypes.Contains(GetOutermostName(name.FullName)))
                continue;

            missingTypes.Add(name);
            // A missing outer type was already listed, and takes its nested types with it.
            var outer = name.FullName.LastIndexOf('+');
            if (outer < 0 || !missingTypes.Contains(name with { FullName = name.FullName[..outer] }))
                missing.Add($"type {name.FullName}");
        }

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (GetTypeName(reader, member.Parent) is not { AssemblyName: { } assemblyName } parent || !targets.ContainsKey(assemblyName) || missingTypes.Contains(parent))
                continue;

            var name = reader.GetString(member.Name);
            var signature = reader.GetBlobReader(member.Signature);
            var header = signature.ReadSignatureHeader();
            if (header.Kind is SignatureKind.Field)
            {
                if (Find(parent, targets, [], type => type.Fields.Contains(name), type => type.GetField(name, AllMembers) is not null) is false)
                    missing.Add($"field {parent.FullName}.{name}");
            }
            else if (header.Kind is SignatureKind.Method)
            {
                var genericParameterCount = header.IsGeneric ? signature.ReadCompressedInteger() : 0;
                var parameterCount = signature.ReadCompressedInteger();
                var found = Find(
                    parent,
                    targets,
                    [],
                    type => type.Methods.Contains((name, parameterCount, genericParameterCount)),
                    type => HasMethod(type, name, parameterCount, genericParameterCount)
                );
                if (found is false)
                    missing.Add($"method {parent.FullName}.{name} with {parameterCount} parameter{(parameterCount is 1 ? "" : "s")}");
            }
        }

        return missing;
    }

    /// <summary>
    ///   Looks for a member on a type and on what it inherits from.
    /// </summary>
    /// <param name="name">The type to start at.</param>
    /// <param name="targets">The assemblies whose metadata can be read.</param>
    /// <param name="visited">The types looked at so far.</param>
    /// <param name="hasMember">Whether a type read from metadata has the member itself.</param>
    /// <param name="runtimeHasMember">Whether a type of the running framework has the member, inherited ones included.</param>
    /// <returns>
    ///   <c>true</c> when found, <c>false</c> when everything was looked at
    ///   and it is not there, <c>null</c> when some part could not be looked at.
    /// </returns>
    private static bool? Find(
        TypeName name,
        IReadOnlyDictionary<string, AssemblySurface> targets,
        HashSet<TypeName> visited,
        Func<TypeSurface, bool> hasMember,
        Func<Type, bool> runtimeHasMember
    )
    {
        if (!visited.Add(name))
            return false;

        if (name.AssemblyName is null)
            return null;

        if (!targets.TryGetValue(name.AssemblyName, out var target))
        {
            // Not one of the assemblies read here, so the framework's, if it is there to ask.
            try
            {
                return Type.GetType($"{name.FullName}, {name.AssemblyName}", throwOnError: false) is { } type ? runtimeHasMember(type) : null;
            }
            catch (Exception ex) when (ex is FileLoadException or FileNotFoundException or BadImageFormatException or TypeLoadException or AmbiguousMatchException)
            {
                return null;
            }
        }

        if (!target.Types.TryGetValue(name.FullName, out var surface))
            return null;

        if (hasMember(surface))
            return true;

        var everythingSeen = true;
        foreach (var baseName in surface.Bases)
        {
            // A base in the same assembly is named without one.
            var found = baseName is { } named
                ? Find(named.AssemblyName is null ? named with { AssemblyName = name.AssemblyName } : named, targets, visited, hasMember, runtimeHasMember)
                : null;
            if (found is true)
                return true;

            everythingSeen &= found is false;
        }

        return everythingSeen ? false : null;
    }

    private static bool HasMethod(Type type, string name, int parameterCount, int genericParameterCount)
    {
        if (name is ".ctor")
            return type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(constructor => constructor.GetParameters().Length == parameterCount);

        var candidates = type.IsInterface ? type.GetInterfaces().Prepend(type) : [type];
        return candidates
            .SelectMany(candidate => candidate.GetMethods(AllMembers))
            .Any(method => method.Name == name &&
                method.GetParameters().Length == parameterCount &&
                (method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0) == genericParameterCount);
    }

    private static string GetOutermostName(string fullName)
        => fullName.IndexOf('+') is var nested and >= 0 ? fullName[..nested] : fullName;

    private static string GetFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);
        var declaringType = definition.GetDeclaringType();
        if (!declaringType.IsNil)
            return $"{GetFullName(reader, declaringType)}+{name}";

        var typeNamespace = reader.GetString(definition.Namespace);
        return typeNamespace.Length > 0 ? $"{typeNamespace}.{name}" : name;
    }

    /// <summary>
    ///   Names the type a handle stands for: a definition, a reference, or a
    ///   constructed generic type, which is named by its definition.
    /// </summary>
    /// <param name="reader">The metadata the handle belongs to.</param>
    /// <param name="handle">The handle.</param>
    /// <returns>The type's name, or <c>null</c> for anything else, such as an array or a type parameter.</returns>
    private static TypeName? GetTypeName(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return new(null, GetFullName(reader, (TypeDefinitionHandle)handle));

            case HandleKind.TypeReference:
            {
                var reference = reader.GetTypeReference((TypeReferenceHandle)handle);
                var name = reader.GetString(reference.Name);
                switch (reference.ResolutionScope.Kind)
                {
                    case HandleKind.AssemblyReference:
                    {
                        var assemblyName = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
                        var typeNamespace = reader.GetString(reference.Namespace);
                        return new(assemblyName, typeNamespace.Length > 0 ? $"{typeNamespace}.{name}" : name);
                    }
                    case HandleKind.TypeReference:
                        return GetTypeName(reader, (TypeReferenceHandle)reference.ResolutionScope) is { } outer
                            ? outer with { FullName = $"{outer.FullName}+{name}" }
                            : null;
                    default:
                        return null;
                }
            }

            case HandleKind.TypeSpecification:
            {
                // GENERICINST (CLASS | VALUETYPE) TypeDefOrRefOrSpecEncoded …
                var signature = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
                if (signature.ReadSignatureTypeCode() is not SignatureTypeCode.GenericTypeInstance)
                    return null;

                signature.ReadByte();
                return GetTypeName(reader, signature.ReadTypeHandle());
            }

            default:
                return null;
        }
    }
}
