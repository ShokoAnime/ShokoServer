using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
///   A MessagePack resolver that reads every string through the
///   <see cref="StringPool"/>, and resolves every other type as the
///   <see cref="StandardResolver"/> does. Strings are written unchanged.
/// </summary>
public sealed class PooledStringResolver : IFormatterResolver
{
    #region Fields

    /// <summary>
    ///   The shared instance.
    /// </summary>
    public static readonly PooledStringResolver Instance = new();

    #endregion

    #region Constructors

    private PooledStringResolver() { }

    #endregion

    #region Methods

    /// <summary>
    ///   Gets the formatter for a type.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>The formatter, or <c>null</c> when the type has none.</returns>
    public IMessagePackFormatter<T>? GetFormatter<T>()
        => FormatterCache<T>.Formatter;

    #endregion

    #region Nested Types

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter = typeof(T) == typeof(string)
            ? (IMessagePackFormatter<T>)(object)PooledStringFormatter.Instance
            : StandardResolver.Instance.GetFormatter<T>();
    }

    // Kept out of the generated resolver, which would read every string with it.
    [ExcludeFormatterFromSourceGeneratedResolver]
    internal sealed class PooledStringFormatter : IMessagePackFormatter<string?>
    {
        /// <summary>
        ///   The most bytes a string of <see cref="StringPool.MaxLength"/>
        ///   characters takes in UTF-8.
        /// </summary>
        private const int MaxPooledBytes = StringPool.MaxLength * 3;

        public static readonly PooledStringFormatter Instance = new();

        /// <summary>
        ///   Writes a string as the standard formatter does.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="value">The string.</param>
        /// <param name="options">The serializer options.</param>
        public void Serialize(ref MessagePackWriter writer, string? value, MessagePackSerializerOptions options)
            => writer.Write(value);

        /// <summary>
        ///   Reads a string, handing back the pooled instance of a short one.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="options">The serializer options.</param>
        /// <returns>The string, or <c>null</c> when nil was written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public string? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            if (reader.TryReadStringSpan(out var span))
                return Pool(span);

            var sequence = reader.ReadStringSequence()!.Value;
            if (sequence.Length > MaxPooledBytes)
                return Encoding.UTF8.GetString(sequence);

            Span<byte> buffer = stackalloc byte[(int)sequence.Length];
            sequence.CopyTo(buffer);
            return Pool(buffer);
        }

        /// <summary>
        ///   Decodes a UTF-8 string, handing back the pooled instance when it is short.
        /// </summary>
        /// <param name="bytes">The UTF-8 bytes.</param>
        /// <returns>The string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static string Pool(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > MaxPooledBytes)
                return Encoding.UTF8.GetString(bytes);

            // A UTF-8 string never has more characters than bytes.
            Span<char> chars = stackalloc char[bytes.Length];
            return StringPool.Get(chars[..Encoding.UTF8.GetChars(bytes, chars)]);
        }
    }

    #endregion
}
