using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProductMasterDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE pettle."Products"
                SET "Unit" = 'PCS'
                WHERE "Unit" IS NULL OR btrim("Unit") = '';
                UPDATE pettle."Products"
                SET "ProductType" = 'Finished'
                WHERE "ProductType" IS NULL OR btrim("ProductType") = '';
                UPDATE pettle."Products"
                SET "PrintName" = "Name"
                WHERE "PrintName" IS NULL OR btrim("PrintName") = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
