using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Streamflix.Migrations
{
    /// <inheritdoc />
    public partial class Cloud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 1,
                column: "FeaturesJson",
                value: "[\"Unlimited access to movies and TV series\",\"SD quality\",\"Ad-free experience\",\"Cancel anytime\"]");

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2,
                column: "FeaturesJson",
                value: "[\"Unlimited access to movies and TV series\",\"HD quality\",\"Ad-free experience\",\"Cancel anytime\"]");

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3,
                column: "FeaturesJson",
                value: "[\"Unlimited access to movies and TV series\",\"4K quality\",\"Ad-free experience\",\"Cancel anytime\"]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 1,
                column: "FeaturesJson",
                value: "[\"Watch on 1 screen\",\"Unlimited access to movies and TV series\",\"SD quality\",\"Ad-free experience\",\"Cancel anytime\"]");

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2,
                column: "FeaturesJson",
                value: "[\"Watch on 2 screens\",\"Unlimited access to movies and TV series\",\"HD quality\",\"Ad-free experience\",\"Cancel anytime\"]");

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3,
                column: "FeaturesJson",
                value: "[\"Watch on 4 screens\",\"Unlimited access to movies and TV series\",\"4K quality\",\"Ad-free experience\",\"Cancel anytime\"]");
        }
    }
}
