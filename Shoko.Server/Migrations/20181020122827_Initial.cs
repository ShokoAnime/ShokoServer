using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            using (var sr = new StreamReader(this.GetType().Assembly.GetManifestResourceStream($"Shoko.Server.Migrations.{migrationBuilder.ActiveProvider}.sql")))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    migrationBuilder.Sql(line);
                }
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AniDB_Anime_Character");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_DefaultImage");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Relation");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Review");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Similar");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Tag");

            migrationBuilder.DropTable(
                name: "AniDB_Anime_Title");

            migrationBuilder.DropTable(
                name: "AniDB_AnimeUpdate");

            migrationBuilder.DropTable(
                name: "AniDB_Character");

            migrationBuilder.DropTable(
                name: "AniDB_Character_Seiyuu");

            migrationBuilder.DropTable(
                name: "AniDB_Episode");

            migrationBuilder.DropTable(
                name: "AniDB_Episode_Title");

            migrationBuilder.DropTable(
                name: "AniDB_File");

            migrationBuilder.DropTable(
                name: "AniDB_GroupStatus");

            migrationBuilder.DropTable(
                name: "AniDB_MylistStats");

            migrationBuilder.DropTable(
                name: "AniDB_Recommendation");

            migrationBuilder.DropTable(
                name: "AniDB_ReleaseGroup");

            migrationBuilder.DropTable(
                name: "AniDB_Review");

            migrationBuilder.DropTable(
                name: "AniDB_Seiyuu");

            migrationBuilder.DropTable(
                name: "AniDB_Tag");

            migrationBuilder.DropTable(
                name: "AniDB_Vote");

            migrationBuilder.DropTable(
                name: "AnimeCharacter");

            migrationBuilder.DropTable(
                name: "AnimeEpisode");

            migrationBuilder.DropTable(
                name: "AnimeEpisode_User");

            migrationBuilder.DropTable(
                name: "AnimeGroup");

            migrationBuilder.DropTable(
                name: "AnimeGroup_User");

            migrationBuilder.DropTable(
                name: "AnimeSeries");

            migrationBuilder.DropTable(
                name: "AnimeSeries_User");

            migrationBuilder.DropTable(
                name: "AnimeStaff");

            migrationBuilder.DropTable(
                name: "AuthTokens");

            migrationBuilder.DropTable(
                name: "BookmarkedAnime");

            migrationBuilder.DropTable(
                name: "CloudAccount");

            migrationBuilder.DropTable(
                name: "CommandRequest");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_MAL");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_Other");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_Trakt_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TraktV2");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDB_Episode_Override");

            migrationBuilder.DropTable(
                name: "CrossRef_AniDB_TvDBV2");

            migrationBuilder.DropTable(
                name: "CrossRef_Anime_Staff");

            migrationBuilder.DropTable(
                name: "CrossRef_CustomTag");

            migrationBuilder.DropTable(
                name: "CrossRef_File_Episode");

            migrationBuilder.DropTable(
                name: "CrossRef_Languages_AniDB_File");

            migrationBuilder.DropTable(
                name: "CrossRef_Subtitles_AniDB_File");

            migrationBuilder.DropTable(
                name: "CustomTag");

            migrationBuilder.DropTable(
                name: "DuplicateFile");

            migrationBuilder.DropTable(
                name: "FileFfdshowPreset");

            migrationBuilder.DropTable(
                name: "FileNameHash");

            migrationBuilder.DropTable(
                name: "GroupFilter");

            migrationBuilder.DropTable(
                name: "GroupFilterCondition");

            migrationBuilder.DropTable(
                name: "IgnoreAnime");

            migrationBuilder.DropTable(
                name: "ImportFolder");

            migrationBuilder.DropTable(
                name: "JMMUser");

            migrationBuilder.DropTable(
                name: "Language");

            migrationBuilder.DropTable(
                name: "MovieDB_Fanart");

            migrationBuilder.DropTable(
                name: "MovieDB_Movie");

            migrationBuilder.DropTable(
                name: "MovieDB_Poster");

            migrationBuilder.DropTable(
                name: "Playlist");

            migrationBuilder.DropTable(
                name: "RenameScript");

            migrationBuilder.DropTable(
                name: "Scan");

            migrationBuilder.DropTable(
                name: "ScanFile");

            migrationBuilder.DropTable(
                name: "ScheduledUpdate");

            migrationBuilder.DropTable(
                name: "Trakt_Episode");

            migrationBuilder.DropTable(
                name: "Trakt_Friend");

            migrationBuilder.DropTable(
                name: "Trakt_Season");

            migrationBuilder.DropTable(
                name: "Trakt_Show");

            migrationBuilder.DropTable(
                name: "TvDB_Episode");

            migrationBuilder.DropTable(
                name: "TvDB_ImageFanart");

            migrationBuilder.DropTable(
                name: "TvDB_ImagePoster");

            migrationBuilder.DropTable(
                name: "TvDB_ImageWideBanner");

            migrationBuilder.DropTable(
                name: "TvDB_Series");

            migrationBuilder.DropTable(
                name: "VideoLocal");

            migrationBuilder.DropTable(
                name: "VideoLocal_Place");

            migrationBuilder.DropTable(
                name: "VideoLocal_User");

            migrationBuilder.DropTable(
                name: "AniDB_Anime");
        }
    }
}
