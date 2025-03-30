using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class DropIsCompletedCol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "WatchHistory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "WatchHistory",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
