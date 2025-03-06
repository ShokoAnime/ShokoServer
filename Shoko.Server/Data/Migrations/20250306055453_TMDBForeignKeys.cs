using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class TMDBForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShowTMDB_ShowID",
                table: "TMDB_Season",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDB_MovieID",
                table: "TMDB_Movie_Crew",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDB_MovieID",
                table: "TMDB_Movie_Cast",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDB_EpisodeID",
                table: "TMDB_Episode_Crew",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDB_EpisodeID",
                table: "TMDB_Episode_Cast",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonTMDB_SeasonID",
                table: "TMDB_Episode",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShowTMDB_ShowID",
                table: "TMDB_Episode",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDB_MovieID",
                table: "TMDB_Company_Entity",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Show_TmdbShowID",
                table: "TMDB_Show",
                column: "TmdbShowID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Season_TmdbSeasonID",
                table: "TMDB_Season",
                column: "TmdbSeasonID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Person_TmdbPersonID",
                table: "TMDB_Person",
                column: "TmdbPersonID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Network_TmdbNetworkID",
                table: "TMDB_Network",
                column: "TmdbNetworkID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie",
                column: "TmdbMovieID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Image_RemoteFileName",
                table: "TMDB_Image",
                column: "RemoteFileName");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode",
                column: "TmdbEpisodeID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Company_TmdbCompanyID",
                table: "TMDB_Company",
                column: "TmdbCompanyID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_Collection_TmdbCollectionID",
                table: "TMDB_Collection",
                column: "TmdbCollectionID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbEpisodeGroupCollectionID");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering",
                column: "TmdbEpisodeGroupCollectionID");

            migrationBuilder.CreateTable(
                name: "TMDB_Image_Entity",
                columns: table => new
                {
                    TMDB_Image_EntityID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemoteFileName = table.Column<string>(type: "TEXT", nullable: false),
                    ImageType = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEntityType = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbEntityID = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedAt = table.Column<string>(type: "DATE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TMDB_Image_Entity", x => x.TMDB_Image_EntityID);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Collection_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Collection",
                        principalColumn: "TmdbCollectionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Company_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Company",
                        principalColumn: "TmdbCompanyID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Episode_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Episode",
                        principalColumn: "TmdbEpisodeID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Image_RemoteFileName",
                        column: x => x.RemoteFileName,
                        principalTable: "TMDB_Image",
                        principalColumn: "RemoteFileName",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Movie_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Movie",
                        principalColumn: "TmdbMovieID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Person_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Person",
                        principalColumn: "TmdbPersonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Season_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Season",
                        principalColumn: "TmdbSeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TMDB_Image_Entity_TMDB_Show_TmdbEntityID",
                        column: x => x.TmdbEntityID,
                        principalTable: "TMDB_Show",
                        principalColumn: "TmdbShowID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Title_ParentID",
                table: "TMDB_Title",
                column: "ParentID");

            migrationBuilder.CreateIndex(
                name: "IX_Tmdb_Show_Network_TmdbNetworkID",
                table: "Tmdb_Show_Network",
                column: "TmdbNetworkID");

            migrationBuilder.CreateIndex(
                name: "IX_Tmdb_Show_Network_TmdbShowID",
                table: "Tmdb_Show_Network",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Season_ShowTMDB_ShowID",
                table: "TMDB_Season",
                column: "ShowTMDB_ShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Overview_ParentID",
                table: "TMDB_Overview",
                column: "ParentID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Crew_TMDB_MovieID",
                table: "TMDB_Movie_Crew",
                column: "TMDB_MovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Movie_Cast_TMDB_MovieID",
                table: "TMDB_Movie_Cast",
                column: "TMDB_MovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Crew_TMDB_EpisodeID",
                table: "TMDB_Episode_Crew",
                column: "TMDB_EpisodeID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_Cast_TMDB_EpisodeID",
                table: "TMDB_Episode_Cast",
                column: "TMDB_EpisodeID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_SeasonTMDB_SeasonID",
                table: "TMDB_Episode",
                column: "SeasonTMDB_SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Episode_ShowTMDB_ShowID",
                table: "TMDB_Episode",
                column: "ShowTMDB_ShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_Entity_TMDB_MovieID",
                table: "TMDB_Company_Entity",
                column: "TMDB_MovieID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_Entity_TmdbEntityID",
                table: "TMDB_Company_Entity",
                column: "TmdbEntityID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Company_Entity_TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity",
                column: "TVShowTMDB_ShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbEpisodeGroupCollectionID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbEpisodeID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbEpisodeID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbShowID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbShowID");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Image_Entity_RemoteFileName",
                table: "TMDB_Image_Entity",
                column: "RemoteFileName");

            migrationBuilder.CreateIndex(
                name: "IX_TMDB_Image_Entity_TmdbEntityID",
                table: "TMDB_Image_Entity",
                column: "TmdbEntityID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering",
                column: "TmdbShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbEpisodeGroupCollectionID",
                principalTable: "TMDB_AlternateOrdering_Season",
                principalColumn: "TmdbEpisodeGroupCollectionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbEpisodeGroupCollectionID",
                principalTable: "TMDB_AlternateOrdering",
                principalColumn: "TmdbEpisodeGroupCollectionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbEpisodeID",
                principalTable: "TMDB_Episode",
                principalColumn: "TmdbEpisodeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering_Episode",
                column: "TmdbShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Season_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbEpisodeGroupCollectionID",
                principalTable: "TMDB_AlternateOrdering",
                principalColumn: "TmdbEpisodeGroupCollectionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_AlternateOrdering_Season_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering_Season",
                column: "TmdbShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Company_TmdbCompanyID",
                table: "TMDB_Company_Entity",
                column: "TmdbCompanyID",
                principalTable: "TMDB_Company",
                principalColumn: "TmdbCompanyID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Company_Entity",
                column: "TMDB_MovieID",
                principalTable: "TMDB_Movie",
                principalColumn: "TMDB_MovieID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Movie_TmdbEntityID",
                table: "TMDB_Company_Entity",
                column: "TmdbEntityID",
                principalTable: "TMDB_Movie",
                principalColumn: "TmdbMovieID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Show_TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity",
                column: "TVShowTMDB_ShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TMDB_ShowID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Show_TmdbEntityID",
                table: "TMDB_Company_Entity",
                column: "TmdbEntityID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_TMDB_Season_SeasonTMDB_SeasonID",
                table: "TMDB_Episode",
                column: "SeasonTMDB_SeasonID",
                principalTable: "TMDB_Season",
                principalColumn: "TMDB_SeasonID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_TMDB_Season_TmdbSeasonID",
                table: "TMDB_Episode",
                column: "TmdbSeasonID",
                principalTable: "TMDB_Season",
                principalColumn: "TmdbSeasonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_TMDB_Show_ShowTMDB_ShowID",
                table: "TMDB_Episode",
                column: "ShowTMDB_ShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TMDB_ShowID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_TMDB_Show_TmdbSeasonID",
                table: "TMDB_Episode",
                column: "TmdbSeasonID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Episode_TMDB_EpisodeID",
                table: "TMDB_Episode_Cast",
                column: "TMDB_EpisodeID",
                principalTable: "TMDB_Episode",
                principalColumn: "TMDB_EpisodeID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode_Cast",
                column: "TmdbEpisodeID",
                principalTable: "TMDB_Episode",
                principalColumn: "TmdbEpisodeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Person_TmdbPersonID",
                table: "TMDB_Episode_Cast",
                column: "TmdbPersonID",
                principalTable: "TMDB_Person",
                principalColumn: "TmdbPersonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Episode_TMDB_EpisodeID",
                table: "TMDB_Episode_Crew",
                column: "TMDB_EpisodeID",
                principalTable: "TMDB_Episode",
                principalColumn: "TMDB_EpisodeID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode_Crew",
                column: "TmdbEpisodeID",
                principalTable: "TMDB_Episode",
                principalColumn: "TmdbEpisodeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Person_TmdbPersonID",
                table: "TMDB_Episode_Crew",
                column: "TmdbPersonID",
                principalTable: "TMDB_Person",
                principalColumn: "TmdbPersonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_TMDB_Collection_TmdbCollectionID",
                table: "TMDB_Movie",
                column: "TmdbCollectionID",
                principalTable: "TMDB_Collection",
                principalColumn: "TmdbCollectionID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Movie_Cast",
                column: "TMDB_MovieID",
                principalTable: "TMDB_Movie",
                principalColumn: "TMDB_MovieID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie_Cast",
                column: "TmdbMovieID",
                principalTable: "TMDB_Movie",
                principalColumn: "TmdbMovieID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Person_TmdbPersonID",
                table: "TMDB_Movie_Cast",
                column: "TmdbPersonID",
                principalTable: "TMDB_Person",
                principalColumn: "TmdbPersonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Movie_Crew",
                column: "TMDB_MovieID",
                principalTable: "TMDB_Movie",
                principalColumn: "TMDB_MovieID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie_Crew",
                column: "TmdbMovieID",
                principalTable: "TMDB_Movie",
                principalColumn: "TmdbMovieID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Person_TmdbPersonID",
                table: "TMDB_Movie_Crew",
                column: "TmdbPersonID",
                principalTable: "TMDB_Person",
                principalColumn: "TmdbPersonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Collection_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Collection",
                principalColumn: "TmdbCollectionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Episode_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Episode",
                principalColumn: "TmdbEpisodeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Movie_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Movie",
                principalColumn: "TmdbMovieID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Person_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Person",
                principalColumn: "TmdbPersonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Season_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Season",
                principalColumn: "TmdbSeasonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Overview_TMDB_Show_ParentID",
                table: "TMDB_Overview",
                column: "ParentID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Season_TMDB_Show_ShowTMDB_ShowID",
                table: "TMDB_Season",
                column: "ShowTMDB_ShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TMDB_ShowID");

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Season_TMDB_Show_TmdbShowID",
                table: "TMDB_Season",
                column: "TmdbShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tmdb_Show_Network_TMDB_Network_TmdbNetworkID",
                table: "Tmdb_Show_Network",
                column: "TmdbNetworkID",
                principalTable: "TMDB_Network",
                principalColumn: "TmdbNetworkID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tmdb_Show_Network_TMDB_Show_TmdbShowID",
                table: "Tmdb_Show_Network",
                column: "TmdbShowID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Title_TMDB_Collection_ParentID",
                table: "TMDB_Title",
                column: "ParentID",
                principalTable: "TMDB_Collection",
                principalColumn: "TmdbCollectionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Title_TMDB_Episode_ParentID",
                table: "TMDB_Title",
                column: "ParentID",
                principalTable: "TMDB_Episode",
                principalColumn: "TmdbEpisodeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Title_TMDB_Movie_ParentID",
                table: "TMDB_Title",
                column: "ParentID",
                principalTable: "TMDB_Movie",
                principalColumn: "TmdbMovieID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Title_TMDB_Season_ParentID",
                table: "TMDB_Title",
                column: "ParentID",
                principalTable: "TMDB_Season",
                principalColumn: "TmdbSeasonID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TMDB_Title_TMDB_Show_ParentID",
                table: "TMDB_Title",
                column: "ParentID",
                principalTable: "TMDB_Show",
                principalColumn: "TmdbShowID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Episode_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Season_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Season");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_AlternateOrdering_Season_TMDB_Show_TmdbShowID",
                table: "TMDB_AlternateOrdering_Season");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Company_TmdbCompanyID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Movie_TmdbEntityID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Show_TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Company_Entity_TMDB_Show_TmdbEntityID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_TMDB_Season_SeasonTMDB_SeasonID",
                table: "TMDB_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_TMDB_Season_TmdbSeasonID",
                table: "TMDB_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_TMDB_Show_ShowTMDB_ShowID",
                table: "TMDB_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_TMDB_Show_TmdbSeasonID",
                table: "TMDB_Episode");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Episode_TMDB_EpisodeID",
                table: "TMDB_Episode_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Cast_TMDB_Person_TmdbPersonID",
                table: "TMDB_Episode_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Episode_TMDB_EpisodeID",
                table: "TMDB_Episode_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Episode_Crew_TMDB_Person_TmdbPersonID",
                table: "TMDB_Episode_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_TMDB_Collection_TmdbCollectionID",
                table: "TMDB_Movie");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Movie_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Cast_TMDB_Person_TmdbPersonID",
                table: "TMDB_Movie_Cast");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Movie_TMDB_MovieID",
                table: "TMDB_Movie_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Movie_Crew_TMDB_Person_TmdbPersonID",
                table: "TMDB_Movie_Crew");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Collection_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Episode_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Movie_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Person_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Season_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Overview_TMDB_Show_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Season_TMDB_Show_ShowTMDB_ShowID",
                table: "TMDB_Season");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Season_TMDB_Show_TmdbShowID",
                table: "TMDB_Season");

            migrationBuilder.DropForeignKey(
                name: "FK_Tmdb_Show_Network_TMDB_Network_TmdbNetworkID",
                table: "Tmdb_Show_Network");

            migrationBuilder.DropForeignKey(
                name: "FK_Tmdb_Show_Network_TMDB_Show_TmdbShowID",
                table: "Tmdb_Show_Network");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Title_TMDB_Collection_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Title_TMDB_Episode_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Title_TMDB_Movie_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Title_TMDB_Season_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropForeignKey(
                name: "FK_TMDB_Title_TMDB_Show_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropTable(
                name: "TMDB_Image_Entity");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Title_ParentID",
                table: "TMDB_Title");

            migrationBuilder.DropIndex(
                name: "IX_Tmdb_Show_Network_TmdbNetworkID",
                table: "Tmdb_Show_Network");

            migrationBuilder.DropIndex(
                name: "IX_Tmdb_Show_Network_TmdbShowID",
                table: "Tmdb_Show_Network");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Show_TmdbShowID",
                table: "TMDB_Show");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Season_TmdbSeasonID",
                table: "TMDB_Season");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Season_ShowTMDB_ShowID",
                table: "TMDB_Season");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Person_TmdbPersonID",
                table: "TMDB_Person");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Overview_ParentID",
                table: "TMDB_Overview");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Network_TmdbNetworkID",
                table: "TMDB_Network");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Movie_Crew_TMDB_MovieID",
                table: "TMDB_Movie_Crew");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Movie_Cast_TMDB_MovieID",
                table: "TMDB_Movie_Cast");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Movie_TmdbMovieID",
                table: "TMDB_Movie");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Image_RemoteFileName",
                table: "TMDB_Image");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Episode_Crew_TMDB_EpisodeID",
                table: "TMDB_Episode_Crew");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Episode_Cast_TMDB_EpisodeID",
                table: "TMDB_Episode_Cast");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Episode_TmdbEpisodeID",
                table: "TMDB_Episode");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Episode_SeasonTMDB_SeasonID",
                table: "TMDB_Episode");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Episode_ShowTMDB_ShowID",
                table: "TMDB_Episode");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Company_Entity_TMDB_MovieID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Company_Entity_TmdbEntityID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_Company_Entity_TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Company_TmdbCompanyID",
                table: "TMDB_Company");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_Collection_TmdbCollectionID",
                table: "TMDB_Collection");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_AlternateOrdering_Season_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Season");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbEpisodeID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropIndex(
                name: "IX_TMDB_AlternateOrdering_Episode_TmdbShowID",
                table: "TMDB_AlternateOrdering_Episode");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TMDB_AlternateOrdering_TmdbEpisodeGroupCollectionID",
                table: "TMDB_AlternateOrdering");

            migrationBuilder.DropColumn(
                name: "ShowTMDB_ShowID",
                table: "TMDB_Season");

            migrationBuilder.DropColumn(
                name: "TMDB_MovieID",
                table: "TMDB_Movie_Crew");

            migrationBuilder.DropColumn(
                name: "TMDB_MovieID",
                table: "TMDB_Movie_Cast");

            migrationBuilder.DropColumn(
                name: "TMDB_EpisodeID",
                table: "TMDB_Episode_Crew");

            migrationBuilder.DropColumn(
                name: "TMDB_EpisodeID",
                table: "TMDB_Episode_Cast");

            migrationBuilder.DropColumn(
                name: "SeasonTMDB_SeasonID",
                table: "TMDB_Episode");

            migrationBuilder.DropColumn(
                name: "ShowTMDB_ShowID",
                table: "TMDB_Episode");

            migrationBuilder.DropColumn(
                name: "TMDB_MovieID",
                table: "TMDB_Company_Entity");

            migrationBuilder.DropColumn(
                name: "TVShowTMDB_ShowID",
                table: "TMDB_Company_Entity");
        }
    }
}
