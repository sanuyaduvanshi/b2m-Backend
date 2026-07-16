using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDaySlabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceDaySlabs",
                schema: "pettle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinDays = table.Column<int>(type: "integer", nullable: false),
                    MaxDays = table.Column<int>(type: "integer", nullable: true),
                    PricePerDay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDaySlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDaySlabs_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalSchema: "pettle",
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDaySlabs_ServiceItemId",
                schema: "pettle",
                table: "ServiceDaySlabs",
                column: "ServiceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDaySlabs",
                schema: "pettle");
        }
    }
}
