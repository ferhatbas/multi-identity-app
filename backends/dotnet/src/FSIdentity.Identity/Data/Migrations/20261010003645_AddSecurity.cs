using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    MinPasswordLength = table.Column<int>(type: "int", nullable: true),
                    PasswordRequiresLetter = table.Column<bool>(type: "bit", nullable: true),
                    PasswordRequiresDigit = table.Column<bool>(type: "bit", nullable: true),
                    PasswordRequiresSymbol = table.Column<bool>(type: "bit", nullable: true),
                    PasswordRequiresMixedCase = table.Column<bool>(type: "bit", nullable: true),
                    PasswordMaxAgeDays = table.Column<int>(type: "int", nullable: true),
                    MaxFailedAttempts = table.Column<int>(type: "int", nullable: true),
                    LockoutWindowMinutes = table.Column<int>(type: "int", nullable: true),
                    LockoutMinutes = table.Column<int>(type: "int", nullable: true),
                    SessionDays = table.Column<int>(type: "int", nullable: true),
                    RequireMfaFromRank = table.Column<int>(type: "int", nullable: true),
                    RequireConfirmedEmail = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IdentityCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    IdentityTenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoDRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    PermissionAId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionBId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IdentityCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    IdentityTenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoDRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoDRules_Permissions_PermissionAId",
                        column: x => x.PermissionAId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoDRules_Permissions_PermissionBId",
                        column: x => x.PermissionBId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityPolicies_IdentityTenantId",
                table: "SecurityPolicies",
                column: "IdentityTenantId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SoDRules_IdentityTenantId_PermissionAId_PermissionBId",
                table: "SoDRules",
                columns: new[] { "IdentityTenantId", "PermissionAId", "PermissionBId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SoDRules_PermissionAId",
                table: "SoDRules",
                column: "PermissionAId");

            migrationBuilder.CreateIndex(
                name: "IX_SoDRules_PermissionBId",
                table: "SoDRules",
                column: "PermissionBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityPolicies");

            migrationBuilder.DropTable(
                name: "SoDRules");
        }
    }
}
