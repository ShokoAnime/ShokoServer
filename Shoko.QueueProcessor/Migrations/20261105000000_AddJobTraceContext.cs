using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                table: "Jobs",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceParent",
                table: "Jobs");
        }
    }
}
