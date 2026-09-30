using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Covers plugin databases on the core's database server: the prefix their tables get, how a
/// migration is renamed to match, which context and backend a registration ends up on, and how
/// the migrator describes a failure there. The backend is read from the settings as the first-run
/// setup left them, when the plugin databases are migrated.
/// </summary>
public sealed class PluginDatabaseBackendTests : IDisposable
{
    private const string SqlServerConnectionString = "Server=127.0.0.1,1;Database=shoko;User ID=shoko;Password=unused;TrustServerCertificate=True";

    private readonly string _root = Path.Join(Path.GetTempPath(), $"shoko-plugin-database-backend-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    #region Naming

    [Fact]
    public void ThePrefixNamesThePluginAndTheDatabase()
    {
        var pluginID = Guid.NewGuid();

        var naming = PluginTableNaming.For(pluginID, "notes", PluginTableNaming.MySqlMaxLength);

        Assert.StartsWith(PluginTableNaming.GetPluginPrefix(pluginID), naming.Prefix);
        Assert.Equal(naming.Prefix, PluginTableNaming.For(pluginID, "Notes", PluginTableNaming.SqlServerMaxLength).Prefix);
        Assert.NotEqual(naming.Prefix, PluginTableNaming.For(pluginID, "notes2", PluginTableNaming.MySqlMaxLength).Prefix);
        Assert.NotEqual(naming.Prefix, PluginTableNaming.For(Guid.NewGuid(), "notes", PluginTableNaming.MySqlMaxLength).Prefix);
        Assert.Equal(naming.Prefix + "Notes", naming.Name("Notes"));
    }

    [Fact]
    public void ANameTooLongForTheServerKeepsItsStartAndEndsInAHash()
    {
        var naming = PluginTableNaming.For(Guid.NewGuid(), "notes", PluginTableNaming.MySqlMaxLength);
        var first = "FK_" + new string('a', 60) + "_First";
        var second = "FK_" + new string('a', 60) + "_Second";

        Assert.Equal(PluginTableNaming.MySqlMaxLength, naming.Name(first).Length);
        Assert.StartsWith(naming.Prefix + "FK_aaa", naming.Name(first));
        Assert.NotEqual(naming.Name(first), naming.Name(second));
        Assert.Equal(naming.Name(first), naming.Name(first));
    }

    [Fact]
    public void AMigrationNamesOnlyPrefixedTablesAndConstraints()
    {
        var naming = new PluginTableNaming("p_", PluginTableNaming.MySqlMaxLength);
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        builder.CreateTable(
            name: "Notes",
            columns: table => new { ID = table.Column<int>(nullable: false), BookID = table.Column<int>(nullable: false) },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notes", row => row.ID);
                table.ForeignKey("FK_Notes_Books_BookID", row => row.BookID, "Books", "ID");
                table.UniqueConstraint("AK_Notes_BookID", row => row.BookID);
            }
        );
        builder.CreateIndex(name: "IX_Notes_BookID", table: "Notes", column: "BookID");
        builder.AddColumn<string>(name: "Tag", table: "Notes", nullable: true);
        builder.InsertData(table: "Notes", columns: ["ID", "BookID"], values: new object[] { 1, 1 });
        builder.RenameTable(name: "Notes", newName: "Entries");
        builder.DropForeignKey(name: "FK_Notes_Books_BookID", table: "Entries");
        builder.Sql("SELECT 1");

        naming.Rewrite(builder.Operations);

        var create = (CreateTableOperation)builder.Operations[0];
        Assert.Equal("p_Notes", create.Name);
        Assert.All(create.Columns, column => Assert.Equal("p_Notes", column.Table));
        Assert.Equal("p_PK_Notes", create.PrimaryKey!.Name);
        Assert.Equal("p_Notes", create.PrimaryKey.Table);
        Assert.Equal(("p_FK_Notes_Books_BookID", "p_Books"), (create.ForeignKeys[0].Name, create.ForeignKeys[0].PrincipalTable));
        Assert.Equal("p_AK_Notes_BookID", create.UniqueConstraints[0].Name);
        Assert.Equal(("IX_Notes_BookID", "p_Notes"), (((CreateIndexOperation)builder.Operations[1]).Name, ((CreateIndexOperation)builder.Operations[1]).Table));
        Assert.Equal("p_Notes", ((AddColumnOperation)builder.Operations[2]).Table);
        Assert.Equal("p_Notes", ((InsertDataOperation)builder.Operations[3]).Table);
        Assert.Equal(("p_Notes", "p_Entries"), (((RenameTableOperation)builder.Operations[4]).Name, ((RenameTableOperation)builder.Operations[4]).NewName));
        Assert.Equal(("p_FK_Notes_Books_BookID", "p_Entries"), (((DropForeignKeyOperation)builder.Operations[5]).Name, ((DropForeignKeyOperation)builder.Operations[5]).Table));
        Assert.Equal("SELECT 1", ((SqlOperation)builder.Operations[6]).Sql);
    }

    [Fact]
    public void AMigrationLeavesTheCoresDatabaseAsItIs()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        builder.AlterDatabase(collation: "utf8mb4_general_ci").Annotation("MySql:CharSet", "utf8mb4");

        new PluginTableNaming("p_", PluginTableNaming.MySqlMaxLength).Rewrite(builder.Operations);

        var alter = (AlterDatabaseOperation)builder.Operations[0];
        Assert.Null(alter.Collation);
        Assert.Empty(alter.GetAnnotations());
    }

    [Fact]
    public void AMigrationOnASchemaIsRefused()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        builder.EnsureSchema("dbo");

        Assert.Throws<NotSupportedException>(() => new PluginTableNaming("p_", PluginTableNaming.SqlServerMaxLength).Rewrite(builder.Operations));
    }

    #endregion

    #region Backend

    [Theory]
    [InlineData(Constants.DatabaseType.SQLite, true, Constants.DatabaseType.SQLite, typeof(NotesContext), 0)]
    [InlineData(Constants.DatabaseType.MySQL, true, Constants.DatabaseType.MySQL, typeof(MySqlNotesContext), PluginTableNaming.MySqlMaxLength)]
    [InlineData(Constants.DatabaseType.MySQL, false, Constants.DatabaseType.SQLite, typeof(NotesContext), 0)]
    [InlineData(Constants.DatabaseType.SQLServer, true, Constants.DatabaseType.SQLServer, typeof(SqlServerNotesContext), PluginTableNaming.SqlServerMaxLength)]
    [InlineData(Constants.DatabaseType.SQLServer, false, Constants.DatabaseType.SQLite, typeof(NotesContext), 0)]
    public void TheDatabaseFollowsTheCoreOnlyWithMigrationsForItsServer(Constants.DatabaseType core, bool withServerMigrations, Constants.DatabaseType expected, Type contextType, int maxLength)
    {
        var pluginID = Guid.NewGuid();
        using var provider = Register(core, pluginID, withServerMigrations);

        var target = Assert.Single(provider.GetServices<IPluginDatabase>()).Target;

        Assert.Equal(expected, target.Type);
        Assert.Equal(contextType, target.ContextType);
        if (maxLength is 0)
        {
            Assert.Null(target.Naming);
            return;
        }

        Assert.Equal(PluginTableNaming.For(pluginID, "notes", maxLength).Prefix, target.Naming!.Prefix);
        Assert.Equal(maxLength, target.Naming.MaxLength);
    }

    [Fact]
    public void TheBackendIsReadFromTheSettingsWhenTheDatabasesAreMigrated()
    {
        // Registered while the first-run setup still has the core on SQLite.
        var settings = new ServerSettings();
        var pluginID = Guid.NewGuid();
        using var provider = Register(new PluginDatabaseServer(new StubSettingsProvider(settings)), pluginID, withServerMigrations: true);
        var database = Assert.Single(provider.GetServices<IPluginDatabase>());

        // The setup then picks SQL Server, before the late start migrates anything.
        settings.Database.Type = Constants.DatabaseType.SQLServer;
        settings.Database.Host = "127.0.0.1,1";
        settings.Database.Schema = "shoko";
        var target = database.Target;

        // And the choice holds for the rest of the run.
        settings.Database.Type = Constants.DatabaseType.SQLite;

        Assert.Equal(Constants.DatabaseType.SQLServer, target.Type);
        Assert.Equal(typeof(SqlServerNotesContext), target.ContextType);
        Assert.Equal(PluginTableNaming.For(pluginID, "notes", PluginTableNaming.SqlServerMaxLength).Prefix, target.Naming!.Prefix);
        Assert.Same(target, database.Target);
        Assert.Equal(Constants.DatabaseType.SQLServer, provider.GetRequiredService<PluginDatabaseServer>().Type);
    }

    [Fact]
    public void OnSqlServerThePluginGetsTheServersContextWithItsTablesPrefixed()
    {
        var pluginID = Guid.NewGuid();
        using var provider = Register(Constants.DatabaseType.SQLServer, pluginID, withServerMigrations: true);
        var prefix = PluginTableNaming.For(pluginID, "notes", PluginTableNaming.SqlServerMaxLength).Prefix;

        using var context = provider.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext();

        Assert.IsType<SqlServerNotesContext>(context);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.Equal(prefix + "Notes", context.Model.FindEntityType(typeof(Note))!.GetTableName());
        Assert.Equal(prefix + "__EFMigrationsHistory", context.GetService<IDbContextOptions>().Extensions.OfType<Microsoft.EntityFrameworkCore.Infrastructure.RelationalOptionsExtension>().Single().MigrationsHistoryTableName);

        // Only the server context's own migrations, renamed as they are created.
        var migrations = context.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(Assert.Single(migrations.Migrations).Value, context.Database.ProviderName!);
        Assert.Equal(prefix + "Notes", ((CreateTableOperation)Assert.Single(migration.UpOperations)).Name);
        Assert.Equal(prefix + "Notes", ((DropTableOperation)Assert.Single(migration.DownOperations)).Name);

        using var scope = provider.CreateScope();
        Assert.IsType<SqlServerNotesContext>(scope.ServiceProvider.GetRequiredService<NotesContext>());
    }

    [Fact]
    public void EachPrefixGetsAModelOfItsOwn()
    {
        using var first = Register(Constants.DatabaseType.SQLServer, Guid.NewGuid(), withServerMigrations: true);
        using var second = Register(Constants.DatabaseType.SQLServer, Guid.NewGuid(), withServerMigrations: true);

        using var firstContext = first.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext();
        using var secondContext = second.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext();

        Assert.NotEqual(firstContext.Model.FindEntityType(typeof(Note))!.GetTableName(), secondContext.Model.FindEntityType(typeof(Note))!.GetTableName());
    }

    [Fact]
    public void ADerivedContextWithoutOptionsOfItsOwnTakesTheRegisteredContextsOptions()
        => Assert.Equal(typeof(DbContextOptions<NotesContext>), PluginDbContextFactory<PluginTestDoubles.TestPlugin, NotesContext>.GetOptionsType(typeof(MySqlNotesContext)));

    [Fact]
    public void AServerNeedsAContextDerivedFromTheRegisteredOne()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes", options => options.WithMySqlMigrations<NotesContext>()));
        Assert.Throws<InvalidOperationException>(() => services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes", options => options
            .WithMySqlMigrations<MySqlNotesContext>()
            .WithSqlServerMigrations<MySqlNotesContext>()));
    }

    #endregion

    #region Migrator

    [Theory]
    [InlineData(Constants.DatabaseType.MySQL, "/backups/notes_20260101000000.sql")]
    [InlineData(Constants.DatabaseType.MySQL, null)]
    [InlineData(Constants.DatabaseType.SQLServer, null)]
    public void AFailureOnAServerNamesTheTablesAndTheDumpInsteadOfAFile(Constants.DatabaseType type, string? dump)
    {
        var contextType = type is Constants.DatabaseType.MySQL ? typeof(MySqlNotesContext) : typeof(SqlServerNotesContext);
        var database = new FakeDatabase(new(type, contextType, new("p12345678_abcdef_", PluginTableNaming.MySqlMaxLength)));

        var ex = new PluginDatabaseMigrationException(database, "20260102000000_Two", dump, new InvalidOperationException("Duplicate column name 'Tag'"));

        Assert.Null(ex.DatabaseFile);
        Assert.Equal((type, "p12345678_abcdef_"), (ex.DatabaseType, ex.TablePrefix));
        Assert.Contains("p12345678_abcdef_", ex.Message);
        if (dump is not null)
            Assert.Contains(dump, ex.Message);
        Assert.DoesNotContain(".db3", ex.Message);
    }

    #endregion

    #region Helpers

    private ServiceProvider Register(Constants.DatabaseType core, Guid pluginID, bool withServerMigrations)
        => Register(new PluginDatabaseServer(core, core is Constants.DatabaseType.SQLite ? string.Empty : SqlServerConnectionString), pluginID, withServerMigrations);

    private ServiceProvider Register(PluginDatabaseServer server, Guid pluginID, bool withServerMigrations)
    {
        var pluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), pluginID, Path.Join(_root, "plugins", "SomePlugin.dll"));
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfo<PluginTestDoubles.TestPlugin>()).Returns(pluginInfo);
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IApplicationPaths>(paths => paths.DataPath == _root && paths.DatabasePath == Path.Join(_root, "data")));
        services.AddSingleton(pluginManager.Object);
        services.AddSingleton(typeof(PluginPaths<>));
        services.AddSingleton(server);
        if (withServerMigrations)
            services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes", options => options
                .WithMySqlMigrations<MySqlNotesContext>()
                .WithSqlServerMigrations<SqlServerNotesContext>());
        else
            services.AddPluginDbContext<PluginTestDoubles.TestPlugin, NotesContext>("notes");
        PluginDatabaseRegistrar.AddPluginDatabases(services);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PluginDatabaseGate>().Open();
        return provider;
    }

    private sealed class FakeDatabase(PluginDatabaseTarget target) : IPluginDatabase
    {
        public Guid PluginID { get; } = Guid.NewGuid();

        public string PluginName => "Fake Plugin";

        public string PluginDllName => "FakePlugin";

        public Type ContextType => typeof(NotesContext);

        public string Name => "notes";

        public string FilePath => Path.Join(Path.GetTempPath(), "shoko-no-such-folder", PluginID.ToString(), "notes.db3");

        public PluginDatabaseTarget Target => target;

        public IReadOnlyList<string> GetPendingMigrations()
            => [];

        public void Migrate(string migration)
            => throw new NotSupportedException();
    }

    public sealed class Note
    {
        public int ID { get; set; }
    }

    public class NotesContext : DbContext
    {
        public NotesContext(DbContextOptions<NotesContext> options) : base(options) { }

        protected NotesContext(DbContextOptions options) : base(options) { }

        public DbSet<Note> Notes => Set<Note>();
    }

    public sealed class MySqlNotesContext(DbContextOptions<NotesContext> options) : NotesContext(options);

    public sealed class SqlServerNotesContext(DbContextOptions<SqlServerNotesContext> options) : NotesContext(options);

    [DbContext(typeof(SqlServerNotesContext))]
    [Migration("20260101000000_CreateNotes")]
    public sealed class CreateNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new { ID = table.Column<int>(nullable: false) },
                constraints: table => table.PrimaryKey("PK_Notes", row => row.ID)
            );

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "Notes");
    }

    #endregion
}
