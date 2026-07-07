using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerIntegrationCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerIntegrationCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeycloakClientUuid = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRotatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisabledBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerIntegrationCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerIntegrationCredentials_ClientId",
                table: "PartnerIntegrationCredentials",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerIntegrationCredentials_IsEnabled",
                table: "PartnerIntegrationCredentials",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerIntegrationCredentials_TenantId",
                table: "PartnerIntegrationCredentials",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerIntegrationCredentials");
        }
    }
}
