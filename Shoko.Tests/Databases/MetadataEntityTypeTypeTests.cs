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
/// Covers <see cref="MetadataEntityTypeType"/>, the NHibernate type that
/// stores a <see cref="MetadataEntityType"/> as its one-byte number.
/// </summary>
[Collection(nameof(MetadataNumberRegistryCollection))]
public class MetadataEntityTypeTypeTests
{
    private const string Column = "EntityType";

    private static object? Read(object? stored, Type columnType)
    {
        var table = new DataTable();
        table.Columns.Add(Column, columnType);
        table.Rows.Add(stored ?? DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        return new MetadataEntityTypeType().NullSafeGet(reader, [Column], null!, null!);
    }

    private static object? Write(MetadataEntityType? entityType)
    {
        using var command = new SqliteCommand();
        command.Parameters.Add(new SqliteParameter());
        new MetadataEntityTypeType().NullSafeSet(command, entityType, 0, null!);

        return command.Parameters[0].Value;
    }

    [Theory]
    [InlineData(typeof(short))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    public void ANumberIsReadWhateverTypeTheDriverHandsBack(Type columnType)
        => Assert.Same(MetadataEntityType.Movie, Read(Convert.ChangeType(MetadataNumberRegistry.GetNumber(MetadataEntityType.Movie), columnType), columnType));

    [Fact]
    public void NullRoundTripsAsDBNull()
    {
        Assert.Equal(DBNull.Value, Write(null));
        Assert.Null(Read(null, typeof(byte)));
    }

    [Fact]
    public void ANewKindSurvivesARoundTrip()
    {
        using var scope = new MetadataNumberRegistryScope();
        var entityType = MetadataEntityType.Register("MettRoundTrip", "mett-round-trip");

        var stored = Write(entityType);

        Assert.Equal(MetadataNumberRegistry.GetNumber(entityType), stored);
        Assert.Same(entityType, Read(stored, typeof(byte)));
    }

    [Fact]
    public void AnUnregisteredKindWithoutANumberIsNotWritten()
        => Assert.Throws<InvalidOperationException>(() => Write(MetadataEntityType.Parse("mett-unregistered")));

    [Fact]
    public void NullIsOnlyEqualToNull()
    {
        var type = new MetadataEntityTypeType();

        Assert.True(type.Equals(null, null));
        Assert.False(type.Equals(MetadataEntityType.Series, null));
        Assert.False(type.Equals(null, MetadataEntityType.Series));
    }
}
