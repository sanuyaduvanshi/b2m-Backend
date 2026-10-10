using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmrRecords",
                schema: "pettle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PetWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Complaint = table.Column<string>(type: "text", nullable: true),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    Advice = table.Column<string>(type: "text", nullable: true),
                    NextVisitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DoctorName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmrRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmrRecords_PetParents_PetParentId",
                        column: x => x.PetParentId,
                        principalSchema: "pettle",
                        principalTable: "PetParents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmrRecords_Pets_PetId",
                        column: x => x.PetId,
                        principalSchema: "pettle",
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmrMedicines",
                schema: "pettle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmrRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "text", nullable: false),
                    Morning = table.Column<string>(type: "text", nullable: true),
                    Afternoon = table.Column<string>(type: "text", nullable: true),
                    Night = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmrMedicines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmrMedicines_EmrRecords_EmrRecordId",
                        column: x => x.EmrRecordId,
                        principalSchema: "pettle",
                        principalTable: "EmrRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmrMedicines_EmrRecordId",
                schema: "pettle",
                table: "EmrMedicines",
                column: "EmrRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmrMedicines_TenantId_EmrRecordId",
                schema: "pettle",
                table: "EmrMedicines",
                columns: new[] { "TenantId", "EmrRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmrRecords_PetId",
                schema: "pettle",
                table: "EmrRecords",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_EmrRecords_PetParentId",
                schema: "pettle",
                table: "EmrRecords",
                column: "PetParentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmrRecords_TenantId_PetId_VisitDate",
                schema: "pettle",
                table: "EmrRecords",
                columns: new[] { "TenantId", "PetId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmrRecords_TenantId_VisitDate",
                schema: "pettle",
                table: "EmrRecords",
                columns: new[] { "TenantId", "VisitDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmrMedicines",
                schema: "pettle");

            migrationBuilder.DropTable(
                name: "EmrRecords",
                schema: "pettle");
        }
    }
}
