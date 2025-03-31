using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class ModifyVideoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentCasts_Content_ContentId",
                table: "ContentCasts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentGenres_Content_ContentId",
                table: "ContentGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistory_Content_ContentId",
                table: "WatchHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchLists_Content_ContentId",
                table: "WatchLists");

            migrationBuilder.DropTable(
                name: "Content");

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    MaturityRating = table.Column<string>(type: "text", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: false),
                    ContentUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentCasts_Videos_ContentId",
                table: "ContentCasts",
                column: "ContentId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentGenres_Videos_ContentId",
                table: "ContentGenres",
                column: "ContentId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistory_Videos_ContentId",
                table: "WatchHistory",
                column: "ContentId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchLists_Videos_ContentId",
                table: "WatchLists",
                column: "ContentId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentCasts_Videos_ContentId",
                table: "ContentCasts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentGenres_Videos_ContentId",
                table: "ContentGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistory_Videos_ContentId",
                table: "WatchHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchLists_Videos_ContentId",
                table: "WatchLists");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.CreateTable(
                name: "Content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    MaturityRating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Content", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentCasts_Content_ContentId",
                table: "ContentCasts",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentGenres_Content_ContentId",
                table: "ContentGenres",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistory_Content_ContentId",
                table: "WatchHistory",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchLists_Content_ContentId",
                table: "WatchLists",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
