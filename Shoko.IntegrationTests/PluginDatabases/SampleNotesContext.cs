using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Shoko.IntegrationTests.PluginDatabases;

/// <summary>
/// A book the sample notes are written in, seeded by the first migration.
/// </summary>
public class SampleBook
{
    public int ID { get; set; }

    /// <summary>
    /// The book's title, unique.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public List<SampleNote> Notes { get; set; } = [];
}

public class SampleNote
{
    public int ID { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The note's tag, added by the second migration.
    /// </summary>
    public string? Tag { get; set; }

    public int BookID { get; set; }

    public SampleBook? Book { get; set; }
}

/// <summary>
/// A plugin database context with migrations for SQLite, and derived contexts
/// with migrations for MySQL and SQL Server, generated with <c>dotnet ef</c>
/// the way a plugin's are. Its tables have a foreign key, a unique index and
/// seed data, so moving them into the core's database renames all of those.
/// </summary>
public class SampleNotesContext : DbContext
{
    public const string SeededBook = "Seeded";

    /// <summary>
    /// Creates the context with its own options.
    /// </summary>
    /// <param name="options">The options.</param>
    public SampleNotesContext(DbContextOptions<SampleNotesContext> options) : base(options) { }

    /// <summary>
    /// Creates a derived context with the options it was given.
    /// </summary>
    /// <param name="options">The options.</param>
    protected SampleNotesContext(DbContextOptions options) : base(options) { }

    public DbSet<SampleBook> Books => Set<SampleBook>();

    public DbSet<SampleNote> Notes => Set<SampleNote>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SampleBook>(book =>
        {
            book.ToTable("Books");
            book.HasKey(row => row.ID);
            book.Property(row => row.Title).HasMaxLength(128);
            book.HasIndex(row => row.Title).IsUnique();
            book.HasData(new SampleBook { ID = 1, Title = SeededBook });
        });

        modelBuilder.Entity<SampleNote>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(row => row.ID);
            note.Property(row => row.Text).HasMaxLength(256);
            note.Property(row => row.Tag).HasMaxLength(64);
            note.HasOne(row => row.Book).WithMany(book => book.Notes).HasForeignKey(row => row.BookID);
        });
    }
}

/// <summary>
/// The sample context on MySQL, taking the registered context's options.
/// </summary>
/// <param name="options">The options.</param>
public class MySqlSampleNotesContext(DbContextOptions<SampleNotesContext> options) : SampleNotesContext(options);

/// <summary>
/// The sample context on SQL Server, taking options of its own.
/// </summary>
/// <param name="options">The options.</param>
public class SqlServerSampleNotesContext(DbContextOptions<SqlServerSampleNotesContext> options) : SampleNotesContext(options);
