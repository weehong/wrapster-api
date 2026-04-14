using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaybillsAndProductReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Waybills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PackagingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WaybillNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PackedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandedOffAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waybills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaybillItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WaybillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBarcode = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaybillItems", x => x.Id);
                    table.CheckConstraint("CK_WaybillItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_WaybillItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaybillItems_Waybills_WaybillId",
                        column: x => x.WaybillId,
                        principalTable: "Waybills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservedQuantity_NonNegative",
                table: "Products",
                sql: "\"ReservedQuantity\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_WaybillItems_ProductId",
                table: "WaybillItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WaybillItems_TenantId_ProductId",
                table: "WaybillItems",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaybillItems_WaybillId_ProductId",
                table: "WaybillItems",
                columns: new[] { "WaybillId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_TenantId_PackagingDate",
                table: "Waybills",
                columns: new[] { "TenantId", "PackagingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_TenantId_Status",
                table: "Waybills",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_TenantId_Status_CreatedAt",
                table: "Waybills",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_TenantId_WaybillNumber",
                table: "Waybills",
                columns: new[] { "TenantId", "WaybillNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaybillItems");

            migrationBuilder.DropTable(
                name: "Waybills");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservedQuantity_NonNegative",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Products");
        }
    }
}
