using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class DashboardLayerScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DashboardLayouts_IdentityTenantId_Scope_ScopeId",
                table: "DashboardLayouts");

            migrationBuilder.AddColumn<long>(
                name: "ApplicationTypeId",
                table: "DashboardLayouts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "DashboardLayouts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardLayouts_IdentityTenantId_Scope_ScopeId_ApplicationTypeId_CompanyId",
                table: "DashboardLayouts",
                columns: new[] { "IdentityTenantId", "Scope", "ScopeId", "ApplicationTypeId", "CompanyId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DashboardLayouts_IdentityTenantId_Scope_ScopeId_ApplicationTypeId_CompanyId",
                table: "DashboardLayouts");

            migrationBuilder.DropColumn(
                name: "ApplicationTypeId",
                table: "DashboardLayouts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "DashboardLayouts");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardLayouts_IdentityTenantId_Scope_ScopeId",
                table: "DashboardLayouts",
                columns: new[] { "IdentityTenantId", "Scope", "ScopeId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
