using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class DropWatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistory_Videos_ContentId",
                table: "WatchHistory");

            migrationBuilder.RenameColumn(
                name: "ContentId",
                table: "WatchHistory",
                newName: "VideoId");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistory_ContentId",
                table: "WatchHistory",
                newName: "IX_WatchHistory_VideoId");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistory_Videos_VideoId",
                table: "WatchHistory",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistory_Videos_VideoId",
                table: "WatchHistory");

            migrationBuilder.RenameColumn(
                name: "VideoId",
                table: "WatchHistory",
                newName: "ContentId");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistory_VideoId",
                table: "WatchHistory",
                newName: "IX_WatchHistory_ContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistory_Videos_ContentId",
                table: "WatchHistory",
                column: "ContentId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
