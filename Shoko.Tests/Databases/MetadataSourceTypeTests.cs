using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Databases.NHibernate;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Covers <see cref="MetadataSourceType"/>, the NHibernate type that stores a
/// <see cref="MetadataSource"/> as its one-byte number.
/// </summary>
[Collection(nameof(MetadataNumberRegistryCollection))]
public class MetadataSourceTypeTests
{
    private const string Column = "Source";

    private static object? Read(object? stored, Type columnType)
    {
        var table = new DataTable();
        table.Columns.Add(Column, columnType);
        table.Rows.Add(stored ?? DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        return new MetadataSourceType().NullSafeGet(reader, [Column], null!, null!);
    }

    private static object? Write(MetadataSource? source)
    {
        using var command = new SqliteCommand();
        command.Parameters.Add(new SqliteParameter());
        new MetadataSourceType().NullSafeSet(command, source, 0, null!);

        return command.Parameters[0].Value;
    }

    [Theory]
    [InlineData(typeof(short))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    public void ANumberIsReadWhateverTypeTheDriverHandsBack(Type columnType)
        => Assert.Same(MetadataSource.TMDB, Read(Convert.ChangeType(MetadataNumberRegistry.GetNumber(MetadataSource.TMDB), columnType), columnType));

    [Fact]
    public void NullRoundTripsAsDBNull()
    {
        Assert.Equal(DBNull.Value, Write(null));
        Assert.Null(Read(null, typeof(byte)));
    }

    [Fact]
    public void ANewSourceSurvivesARoundTrip()
    {
        using var scope = new MetadataNumberRegistryScope();
        var source = MetadataSource.Register("MsttRoundTrip", "mstt-round-trip");

        var stored = Write(source);

        Assert.Equal(MetadataNumberRegistry.GetNumber(source), stored);
        Assert.Same(source, Read(stored, typeof(byte)));
    }

    [Fact]
    public void AnUnregisteredSourceWithoutANumberIsNotWritten()
        => Assert.Throws<InvalidOperationException>(() => Write(MetadataSource.Parse("mstt-unregistered")));

    [Fact]
    public void NullIsOnlyEqualToNull()
    {
        var type = new MetadataSourceType();

        Assert.True(type.Equals(null, null));
        Assert.False(type.Equals(MetadataSource.AniDB, null));
        Assert.False(type.Equals(null, MetadataSource.AniDB));
    }
}
