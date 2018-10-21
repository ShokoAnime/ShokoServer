using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Migrations
{
    public partial class RemovePlexContracts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlexContractBlob",
                table: "AnimeSeries_User");

            migrationBuilder.DropColumn(
                name: "PlexContractSize",
                table: "AnimeSeries_User");

            migrationBuilder.DropColumn(
                name: "PlexContractVersion",
                table: "AnimeSeries_User");

            migrationBuilder.DropColumn(
                name: "PlexContractBlob",
                table: "AnimeGroup_User");

            migrationBuilder.DropColumn(
                name: "PlexContractSize",
                table: "AnimeGroup_User");

            migrationBuilder.DropColumn(
                name: "PlexContractVersion",
                table: "AnimeGroup_User");

            migrationBuilder.DropColumn(
                name: "PlexContractBlob",
                table: "AnimeEpisode");

            migrationBuilder.DropColumn(
                name: "PlexContractSize",
                table: "AnimeEpisode");

            migrationBuilder.DropColumn(
                name: "PlexContractVersion",
                table: "AnimeEpisode");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PlexContractBlob",
                table: "AnimeSeries_User",
                type: "mediumblob",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlexContractSize",
                table: "AnimeSeries_User",
                nullable: false,
                defaultValueSql: "'0'");

            migrationBuilder.AddColumn<int>(
                name: "PlexContractVersion",
                table: "AnimeSeries_User",
                nullable: false,
                defaultValueSql: "'0'");

            migrationBuilder.AddColumn<byte[]>(
                name: "PlexContractBlob",
                table: "AnimeGroup_User",
                type: "mediumblob",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlexContractSize",
                table: "AnimeGroup_User",
                nullable: false,
                defaultValueSql: "'0'");

            migrationBuilder.AddColumn<int>(
                name: "PlexContractVersion",
                table: "AnimeGroup_User",
                nullable: false,
                defaultValueSql: "'0'");

            migrationBuilder.AddColumn<byte[]>(
                name: "PlexContractBlob",
                table: "AnimeEpisode",
                type: "mediumblob",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlexContractSize",
                table: "AnimeEpisode",
                nullable: false,
                defaultValueSql: "'0'");

            migrationBuilder.AddColumn<int>(
                name: "PlexContractVersion",
                table: "AnimeEpisode",
                nullable: false,
                defaultValueSql: "'0'");
        }
    }
}
