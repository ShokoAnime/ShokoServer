using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Migrations
{
    public partial class RemovePlexContracts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                //I hate SQLite for not supporting the drop column clause.
                //This should cover the issue itself though.
                migrationBuilder.Sql("ALTER TABLE AnimeSeries_User RENAME TO AnimeSeries_UserOld; CREATE TABLE AnimeSeries_User(AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL,  AnimeSeriesID int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL,  WatchedDate timestamp NULL,  PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL); INSERT INTO AnimeSeries_User (AnimeSeries_UserID, JMMUserID, AnimeSeriesID, WatchedEpisodeCount, WatchedDate, PlayedCount, WatchedCount, StoppedCount) SELECT AnimeSeries_UserID, JMMUserID, AnimeSeriesID, WatchedEpisodeCount, WatchedDate, PlayedCount, WatchedCount, StoppedCount FROM AnimeSeries_UserOld; DROP TABLE AnimeSeries_UserOld; CREATE UNIQUE INDEX UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID);");
                migrationBuilder.Sql("ALTER TABLE AnimeGroup_User RENAME TO AnimeGroup_UserOld; CREATE TABLE AnimeGroup_User(AnimeGroup_UserID INTEGER PRIMARY KEY AUTOINCREMENT, JMMUserID int NOT NULL, AnimeGroupID int NOT NULL, IsFave int NOT NULL, UnwatchedEpisodeCount int NOT NULL, WatchedEpisodeCount int NOT NULL, WatchedDate timestamp NULL, PlayedCount int NOT NULL, WatchedCount int NOT NULL, StoppedCount int NOT NULL); INSERT INTO AnimeGroup_User (AnimeGroup_UserID, JMMUserID, AnimeGroupID, IsFave, UnwatchedEpisodeCount, WatchedEpisodeCount, WatchedDate, PlayedCount, WatchedCount, StoppedCount) SELECT AnimeGroup_UserID, JMMUserID, AnimeGroupID, IsFave, UnwatchedEpisodeCount, WatchedEpisodeCount, WatchedDate, PlayedCount, WatchedCount, StoppedCount FROM AnimeGroup_UserOld; DROP TABLE AnimeGroup_UserOld; CREATE UNIQUE INDEX UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID);");
                migrationBuilder.Sql("ALTER TABLE AnimeEpisode RENAME TO AnimeEpisodeOld; CREATE TABLE AnimeEpisode(AnimeEpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, AnimeSeriesID int NOT NULL, AniDB_EpisodeID int NOT NULL, DateTimeUpdated timestamp NOT NULL, DateTimeCreated timestamp NOT NULL); INSERT INTO AnimeEpisode (AnimeEpisodeID, AnimeSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated) SELECT AnimeEpisodeID, AnimeSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated FROM AnimeEpisodeOld; DROP TABLE AnimeEpisodeOld; CREATE UNIQUE INDEX UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID); CREATE INDEX IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID);"); 
                return;
            }

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
