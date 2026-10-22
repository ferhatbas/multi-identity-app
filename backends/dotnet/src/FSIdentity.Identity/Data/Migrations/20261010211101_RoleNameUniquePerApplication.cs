using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoleNameUniquePerApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_IdentityTenantId_Name",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IdentityTenantId_ApplicationTypeId_Name",
                table: "Roles",
                columns: new[] { "IdentityTenantId", "ApplicationTypeId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_IdentityTenantId_ApplicationTypeId_Name",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IdentityTenantId_Name",
                table: "Roles",
                columns: new[] { "IdentityTenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
