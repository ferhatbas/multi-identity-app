using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImpersonation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ImpersonatorUserId",
                table: "UserSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImpersonatedBy",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpersonatorUserId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ImpersonatedBy",
                table: "AuditLogs");
        }
    }
}
