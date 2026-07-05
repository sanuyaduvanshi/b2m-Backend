using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkuBatchMrpSellingPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Mrp",
                schema: "pettle",
                table: "SkuBatches",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                schema: "pettle",
                table: "SkuBatches",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mrp",
                schema: "pettle",
                table: "SkuBatches");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                schema: "pettle",
                table: "SkuBatches");
        }
    }
}
