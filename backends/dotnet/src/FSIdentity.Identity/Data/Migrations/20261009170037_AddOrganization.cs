using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Website = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Address2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    City = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    State = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Zip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    CountryId = table.Column<long>(type: "bigint", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    CompanyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IdentityTenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_CompanyTypes_CompanyTypeId",
                        column: x => x.CompanyTypeId,
                        principalTable: "CompanyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Companies_Tenants_IdentityTenantId",
                        column: x => x.IdentityTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CompanyTypeId",
                table: "Companies",
                column: "CompanyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CountryId",
                table: "Companies",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_IdentityTenantId_Code",
                table: "Companies",
                columns: new[] { "IdentityTenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTypes_Code",
                table: "CompanyTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "CompanyTypes");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
