using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shoko.Abstractions.Plugin;

namespace Shoko.IntegrationTests;

/// <summary>
/// Real plugin DLLs, compiled from source and put in the plugins folder before the server starts,
/// so the server loads them the way it loads any installed plugin.
/// </summary>
/// <remarks>
/// Each plugin has a <c>RestartCaller</c> class whose static methods call
/// <c>ISystemService.RequireRestart</c> for their plugin from the plugin's own code: <c>Direct</c>
/// right away and <c>AfterAwait</c> after yielding the thread. The folder plugin also has
/// <c>ThroughLibrary</c>, which makes the call from a library shipped in the plugin's folder.
/// </remarks>
public static class TestPlugins
{
    #region Plugins

    /// <summary>
    /// The ID of the plugin installed in a folder of its own, with a library beside it.
    /// </summary>
    public static readonly Guid FolderPluginID = new("5d3e8b52-6c2a-4f41-9a1e-0b7f4c1d2e61");

    public const string FolderPluginAssembly = "Shoko.IntegrationTests.FolderPlugin";

    /// <summary>
    /// The name of the library shipped in the folder plugin's folder.
    /// </summary>
    private const string FolderLibraryAssembly = "Shoko.IntegrationTests.FolderPlugin.Library";

    /// <summary>
    /// The ID of the plugin installed as a single DLL in the root of the plugins folder.
    /// </summary>
    public static readonly Guid SingleFilePluginID = new("a4c07e19-3b8d-4e6f-8c25-71d9e0f3b4a8");

    public const string SingleFilePluginAssembly = "Shoko.IntegrationTests.SingleFilePlugin";

    /// <summary>
    /// The ID of the plugin with a database of its own, installed as a single DLL.
    /// </summary>
    public static readonly Guid DatabasePluginID = new("c2f1d8a4-7e36-4b59-9d0a-3e5b6c7f8a91");

    public const string DatabasePluginAssembly = "Shoko.IntegrationTests.DatabasePlugin";

    public const string DatabaseName = "notes";

    /// <summary>
    /// The database plugin's two migrations, in the order they apply.
    /// </summary>
    public static readonly string[] DatabaseMigrations = ["20260901000000_CreateNotes", "20260902000000_AddTag"];

    #endregion

    #region Install

    /// <summary>
    /// Compiles the test plugins and writes them to the plugins folder.
    /// </summary>
    /// <param name="pluginsPath">The server's plugins folder.</param>
    /// <exception cref="InvalidOperationException">A plugin failed to compile.</exception>
    public static void Install(string pluginsPath)
    {
        var folder = Path.Combine(pluginsPath, FolderPluginAssembly);
        Directory.CreateDirectory(folder);

        var libraryPath = Path.Combine(folder, $"{FolderLibraryAssembly}.dll");
        Compile(FolderLibraryAssembly, LibrarySource, [], libraryPath);
        Compile(FolderPluginAssembly, PluginSource(FolderPluginAssembly, FolderPluginID, withLibrary: true), [libraryPath], Path.Combine(folder, $"{FolderPluginAssembly}.dll"));
        Compile(SingleFilePluginAssembly, PluginSource(SingleFilePluginAssembly, SingleFilePluginID, withLibrary: false), [], Path.Combine(pluginsPath, $"{SingleFilePluginAssembly}.dll"));
        Compile(DatabasePluginAssembly, DatabasePluginSource, [], Path.Combine(pluginsPath, $"{DatabasePluginAssembly}.dll"));
    }

    private static void Compile(string assemblyName, string source, IEnumerable<string> extraReferences, string outputPath)
    {
        // Every assembly the test host can load, the abstractions included, the way a plugin build
        // would see the framework and the abstractions package.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(IPlugin).Assembly.Location)
            .Concat(extraReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable)
        );

        using var stream = File.Create(outputPath);
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException($"Could not compile {assemblyName}: {string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error))}");
    }

    #endregion

    #region Sources

    private const string LibrarySource =
        """
        using Shoko.Abstractions.Core;
        using Shoko.Abstractions.Core.Services;
        using Shoko.Abstractions.Plugin;

        [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

        namespace Shoko.IntegrationTests.FolderPlugin.Library;

        public static class LibraryCaller
        {
            public static IRestartRequirement RequireRestart<TPlugin>(ISystemService systemService, string description) where TPlugin : class, IPlugin
                => systemService.RequireRestart<TPlugin>(description);
        }
        """;

    private const string ThroughLibrarySource =
        """
            public static IRestartRequirement ThroughLibrary(ISystemService systemService, string description)
                => Shoko.IntegrationTests.FolderPlugin.Library.LibraryCaller.RequireRestart<Plugin>(systemService, description);
        """;

    private static string PluginSource(string assemblyName, Guid id, bool withLibrary)
        => $$"""
            using System;
            using System.Threading.Tasks;
            using Shoko.Abstractions.Core;
            using Shoko.Abstractions.Core.Services;
            using Shoko.Abstractions.Plugin;

            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyMetadata("PackageID", "{{id}}")]
            [assembly: System.Reflection.AssemblyMetadata("PackageName", "{{assemblyName}}")]

            namespace {{assemblyName}};

            public class Plugin : IPlugin
            {
                public Guid ID => new("{{id}}");

                public string Name => "{{assemblyName}}";
            }

            public static class RestartCaller
            {
                public static IRestartRequirement Direct(ISystemService systemService, string description)
                    => systemService.RequireRestart<Plugin>(description);

                public static async Task<IRestartRequirement> AfterAwait(ISystemService systemService, string description)
                {
                    await Task.Yield();
                    return systemService.RequireRestart<Plugin>(description);
                }

            {{(withLibrary ? ThroughLibrarySource : string.Empty)}}
            }
            """;

    private const string NotesModelSource =
        """
            public class Note
            {
                public int ID { get; set; }

                public string Text { get; set; } = string.Empty;

                public string? Tag { get; set; }
            }

            [DbContext(typeof(NotesContext))]
            [Migration("20260901000000_CreateNotes")]
            public class CreateNotes : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                    => migrationBuilder.CreateTable(
                        name: "Notes",
                        columns: table => new
                        {
                            ID = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                            Text = table.Column<string>(type: "TEXT", nullable: false),
                        },
                        constraints: table => table.PrimaryKey("PK_Notes", x => x.ID)
                    );
            }

            [DbContext(typeof(NotesContext))]
            [Migration("20260902000000_AddTag")]
            public class AddTag : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                    => migrationBuilder.AddColumn<string>(name: "Tag", table: "Notes", type: "TEXT", nullable: true);
            }

            [DbContext(typeof(NotesContext))]
            public class NotesContextModelSnapshot : ModelSnapshot
            {
                protected override void BuildModel(ModelBuilder modelBuilder)
                {
                    modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
                    modelBuilder.Entity("NOTE_TYPE", b =>
                    {
                        b.Property<int>("ID").ValueGeneratedOnAdd().HasColumnType("INTEGER");
                        b.Property<string>("Tag").HasColumnType("TEXT");
                        b.Property<string>("Text").IsRequired().HasColumnType("TEXT");
                        b.HasKey("ID");
                        b.ToTable("Notes");
                    });
                }
            }
        """;

    private const string NotesUsings =
        """
        using System;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Infrastructure;
        using Microsoft.EntityFrameworkCore.Migrations;
        using Microsoft.Extensions.DependencyInjection;
        using Shoko.Abstractions.Plugin;
        """;

    private static string DatabasePluginSource
        => $$"""
            {{NotesUsings}}
            using System.Linq;
            using Shoko.Abstractions.Core.Services;

            [assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
            [assembly: System.Reflection.AssemblyMetadata("PackageID", "{{DatabasePluginID}}")]
            [assembly: System.Reflection.AssemblyMetadata("PackageName", "{{DatabasePluginAssembly}}")]

            namespace {{DatabasePluginAssembly}};

            public class Plugin : IPlugin, IPluginServiceRegistration
            {
                private IServiceProvider? _services;

                public Guid ID => new("{{DatabasePluginID}}");

                public string Name => "{{DatabasePluginAssembly}}";

                // What asking for the database gave, in Ready and in AboutToStart.
                public static string? OnReady { get; private set; }

                public static string? OnAboutToStart { get; private set; }

                public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
                    => serviceCollection.AddPluginDbContext<Plugin, NotesContext>("{{DatabaseName}}");

                public void Setup(IServiceProvider serviceProvider)
                {
                    _services = serviceProvider;
                    serviceProvider.GetRequiredService<ISystemService>().AboutToStart += (_, _) => OnAboutToStart = Ask();
                }

                public void Ready()
                    => OnReady = Ask();

                private string Ask()
                {
                    try
                    {
                        using var context = _services!.GetRequiredService<IDbContextFactory<NotesContext>>().CreateDbContext();
                        return $"{context.Database.GetAppliedMigrations().Count()} migrations";
                    }
                    catch (Exception ex)
                    {
                        return ex.GetType().Name;
                    }
                }
            }

            public class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
            {
                public DbSet<Note> Notes => Set<Note>();
            }

            {{NotesModelSource.Replace("NOTE_TYPE", $"{DatabasePluginAssembly}.Note")}}
            """;

    #endregion
}
