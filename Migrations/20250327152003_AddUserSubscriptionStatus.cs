using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubscriptionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UserSubscription");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "UserSubscription",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserSubscription");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UserSubscription",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
