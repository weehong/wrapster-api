using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUsernameAndAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "AuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_Action_TenantId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityName", "Action", "TenantId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityName_Action_TenantId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "AuditLogs");
        }
    }
}
