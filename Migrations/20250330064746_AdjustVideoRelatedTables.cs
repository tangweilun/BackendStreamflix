using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class AdjustVideoRelatedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentCasts");

            migrationBuilder.DropTable(
                name: "ContentGenres");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Genres");

            migrationBuilder.AlterColumn<string>(
                name: "GenreName",
                table: "Genres",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Actors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateTable(
                name: "VideoCasts",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCasts", x => new { x.VideoId, x.ActorId });
                    table.ForeignKey(
                        name: "FK_VideoCasts_Actors_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoCasts_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoGenres",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "integer", nullable: false),
                    GenreId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoGenres", x => new { x.VideoId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_VideoGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoGenres_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoCasts_ActorId",
                table: "VideoCasts",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoGenres_GenreId",
                table: "VideoGenres",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoCasts");

            migrationBuilder.DropTable(
                name: "VideoGenres");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Videos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "GenreName",
                table: "Genres",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Genres",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Actors",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ContentCasts",
                columns: table => new
                {
                    ContentId = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCasts", x => new { x.ContentId, x.ActorId });
                    table.ForeignKey(
                        name: "FK_ContentCasts_Actors_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentCasts_Videos_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentGenres",
                columns: table => new
                {
                    ContentId = table.Column<int>(type: "integer", nullable: false),
                    GenreId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentGenres", x => new { x.ContentId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_ContentGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentGenres_Videos_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentCasts_ActorId",
                table: "ContentCasts",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenres_GenreId",
                table: "ContentGenres",
                column: "GenreId");
        }
    }
}
