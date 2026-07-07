using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeWaybillNumberGloballyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fails with SQLSTATE 23505 if the same waybill number exists under multiple
            // tenants; run scripts/2026-07-cleanup-duplicate-waybills.sql first.
            migrationBuilder.DropIndex(
                name: "IX_Waybills_TenantId_WaybillNumber",
                table: "Waybills");

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_WaybillNumber",
                table: "Waybills",
                column: "WaybillNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waybills_WaybillNumber",
                table: "Waybills");

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_TenantId_WaybillNumber",
                table: "Waybills",
                columns: new[] { "TenantId", "WaybillNumber" },
                unique: true);
        }
    }
}
