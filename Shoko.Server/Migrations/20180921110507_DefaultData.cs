using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.Server.Migrations
{
    public partial class DefaultData : Migration
    {
        string renamer = 
            "// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv\n" +
            "// Sub group name\nDO ADD '[%grp] '\n" +
            "// Anime Name, use english name if it exists, otherwise use the Romaji name\n" +
            "IF I(eng) DO ADD '%eng '\n" +
            "IF I(ann);I(!eng) DO ADD '%ann '\n" +
            "// Episode Number, don't use episode number for movies\n" +
            "IF T(!Movie) DO ADD '- %enr'\n" +
            "// If the file version is v2 or higher add it here\n" +
            "IF F(!1) DO ADD 'v%ver'\n" +
            "// Video Resolution\n" +
            "DO ADD ' (%res'\n" +
            "// Video Source (only if blu-ray or DVD)\n" +
            "IF R(DVD),R(Blu-ray) DO ADD ' %src'\n" +
            "// Video Codec\n" +
            "DO ADD ' %vid'\n" +
            "// Video Bit Depth (only if 10bit)\n" +
            "IF Z(10) DO ADD ' %bitbit'\n" +
            "DO ADD ') '\n" +
            "DO ADD '[%CRC]'\n" +
            "\n" +
            "// Replacement rules (cleanup)\n" +
            "DO REPLACE ' ' '_' // replace spaces with underscores\n" +
            "DO REPLACE 'H264/AVC' 'H264'\n" +
            "DO REPLACE '0x0' ''\n" +
            "DO REPLACE '__' '_'\n" +
            "DO REPLACE '__' '_'\n" +
            "\n" +
            "// Replace all illegal file name characters\n" +
            "DO REPLACE '<' '('\n" +
            "DO REPLACE '>' ')'\n" +
            "DO REPLACE ':' '-'\n" +
            "DO REPLACE '\"' '`'\n" +
            "DO REPLACE '/' '_'\n" +
            "DO REPLACE '/' '_'\n" +
            "DO REPLACE '\' '_'\n" +
            "DO REPLACE '|' '_'\n" +
            "DO REPLACE '?' '_'\n" +
            "DO REPLACE '*' '_'";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData("JMMUser",
                new[]
                    { "JMMUserID", "Username", "Password", "IsAdmin", "IsAniDBUser", "IsTraktUser", "HideCategories", "CanEditServerSettings", "PlexUsers", "PlexToken" },
                new object[,]
                {
                    { 1, Shoko.Commons.Properties.Resources.Users_Default, string.Empty, 1, 1, 1, "", 1, null, null},
                    { 2, Shoko.Commons.Properties.Resources.Users_FamilyFriendly, string.Empty, 1, 1, 1, "ecchi,nudity,sex,sexual abuse,horror,erotic game,incest,18 restricted", 1, null, null}
                });

            migrationBuilder.InsertData("RenameScript", 
                new string[] { "RenameScriptID", "ScriptName", "Script", "IsEnabledOnImport", "RenamerType", "ExtraData" },
                new object[] {                1,    "Default",  renamer,                   0,      "Legacy",        null }
                );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("JMMUser", "JMMUserID", new object[] { 1, 2 });
        }
    }
}
