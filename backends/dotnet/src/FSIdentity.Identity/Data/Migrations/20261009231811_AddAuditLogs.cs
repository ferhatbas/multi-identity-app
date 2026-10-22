using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    AuditTenantId = table.Column<long>(type: "bigint", nullable: true),
                    AuditCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Latin1_General_100_CI_AI"),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Latin1_General_100_CI_AI"),
                    ChangedFields = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true, collation: "Latin1_General_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AuditTenantId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "AuditTenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
