using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkuAppListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppImageUrl",
                schema: "pettle",
                table: "Skus",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsListedInApp",
                schema: "pettle",
                table: "Skus",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppImageUrl",
                schema: "pettle",
                table: "Skus");

            migrationBuilder.DropColumn(
                name: "IsListedInApp",
                schema: "pettle",
                table: "Skus");
        }
    }
}
