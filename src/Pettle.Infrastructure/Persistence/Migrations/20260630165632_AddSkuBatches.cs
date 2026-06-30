using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkuBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkuBatches",
                schema: "pettle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkuId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QtyRemaining = table.Column<decimal>(type: "numeric", nullable: false),
                    LandingCost = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkuBatches_Skus_SkuId",
                        column: x => x.SkuId,
                        principalSchema: "pettle",
                        principalTable: "Skus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkuBatches_SkuId",
                schema: "pettle",
                table: "SkuBatches",
                column: "SkuId");

            migrationBuilder.CreateIndex(
                name: "IX_SkuBatches_TenantId_SkuId",
                schema: "pettle",
                table: "SkuBatches",
                columns: new[] { "TenantId", "SkuId" });

            migrationBuilder.CreateIndex(
                name: "IX_SkuBatches_TenantId_SkuId_ExpiryDate",
                schema: "pettle",
                table: "SkuBatches",
                columns: new[] { "TenantId", "SkuId", "ExpiryDate" });

            // Seed opening-balance batches for all SKUs that already have stock
            migrationBuilder.Sql(@"
INSERT INTO pettle.""SkuBatches""
    (""Id"", ""TenantId"", ""BranchId"", ""SkuId"", ""BatchNumber"", ""ExpiryDate"",
     ""QtyRemaining"", ""LandingCost"", ""PurchaseOrderId"", ""Source"",
     ""ReceivedAt"", ""CreatedAt"", ""UpdatedAt"", ""CreatedById"", ""UpdatedById"")
SELECT
    gen_random_uuid(), ""TenantId"", ""BranchId"", ""Id"", NULL, NULL,
    ""StockOnHand"", ""CostPrice"", NULL, 'Opening',
    NOW(), NOW(), NULL, NULL, NULL
FROM pettle.""Skus""
WHERE ""IsDeleted"" = false AND ""StockOnHand"" > 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkuBatches",
                schema: "pettle");
        }
    }
}
