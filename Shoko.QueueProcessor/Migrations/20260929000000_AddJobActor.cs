using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddJobActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActorUserId",
                table: "Jobs",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorDeviceName",
                table: "Jobs",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ActorDeviceName",
                table: "Jobs");
        }
    }
}
