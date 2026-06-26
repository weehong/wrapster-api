using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorRealmAndActorName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "AuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRealm",
                table: "AuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorRealm",
                table: "AuditLogs");
        }
    }
}
