using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageItemKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddOnCatalogueId",
                schema: "pettle",
                table: "SubscriptionPackageServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemKind",
                schema: "pettle",
                table: "SubscriptionPackageServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddOnCatalogueId",
                schema: "pettle",
                table: "SubscriptionPackageServices");

            migrationBuilder.DropColumn(
                name: "ItemKind",
                schema: "pettle",
                table: "SubscriptionPackageServices");
        }
    }
}
