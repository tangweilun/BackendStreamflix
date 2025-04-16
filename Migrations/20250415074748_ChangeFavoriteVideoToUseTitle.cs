using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class ChangeFavoriteVideoToUseTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteVideos_Users_UserId",
                table: "FavoriteVideos");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteVideos_Videos_VideoId",
                table: "FavoriteVideos");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteVideos_VideoId",
                table: "FavoriteVideos");

            migrationBuilder.DropColumn(
                name: "VideoId",
                table: "FavoriteVideos");

            migrationBuilder.AddColumn<string>(
                name: "VideoTitle",
                table: "FavoriteVideos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Videos_Title",
                table: "Videos",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_Title",
                table: "Videos",
                column: "Title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteVideos_VideoTitle",
                table: "FavoriteVideos",
                column: "VideoTitle");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteVideos_Users_UserId",
                table: "FavoriteVideos",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteVideos_Videos_VideoTitle",
                table: "FavoriteVideos",
                column: "VideoTitle",
                principalTable: "Videos",
                principalColumn: "Title",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteVideos_Users_UserId",
                table: "FavoriteVideos");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteVideos_Videos_VideoTitle",
                table: "FavoriteVideos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Videos_Title",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_Title",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteVideos_VideoTitle",
                table: "FavoriteVideos");

            migrationBuilder.DropColumn(
                name: "VideoTitle",
                table: "FavoriteVideos");

            migrationBuilder.AddColumn<int>(
                name: "VideoId",
                table: "FavoriteVideos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteVideos_VideoId",
                table: "FavoriteVideos",
                column: "VideoId");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteVideos_Users_UserId",
                table: "FavoriteVideos",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteVideos_Videos_VideoId",
                table: "FavoriteVideos",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
