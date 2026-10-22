using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ApplicationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppModules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_CI_AI"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsCore = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AppModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyTypeApplicationTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyTypeId = table.Column<long>(type: "bigint", nullable: false),
                    ApplicationTypeId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CompanyTypeApplicationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyTypeApplicationTypes_ApplicationTypes_ApplicationTypeId",
                        column: x => x.ApplicationTypeId,
                        principalTable: "ApplicationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyTypeApplicationTypes_CompanyTypes_CompanyTypeId",
                        column: x => x.CompanyTypeId,
                        principalTable: "CompanyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationTypeModules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationTypeId = table.Column<long>(type: "bigint", nullable: false),
                    AppModuleId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_ApplicationTypeModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationTypeModules_AppModules_AppModuleId",
                        column: x => x.AppModuleId,
                        principalTable: "AppModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationTypeModules_ApplicationTypes_ApplicationTypeId",
                        column: x => x.ApplicationTypeId,
                        principalTable: "ApplicationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTypeModules_ApplicationTypeId_AppModuleId",
                table: "ApplicationTypeModules",
                columns: new[] { "ApplicationTypeId", "AppModuleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTypeModules_AppModuleId",
                table: "ApplicationTypeModules",
                column: "AppModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTypes_Code",
                table: "ApplicationTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppModules_Code",
                table: "AppModules",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTypeApplicationTypes_ApplicationTypeId",
                table: "CompanyTypeApplicationTypes",
                column: "ApplicationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyTypeApplicationTypes_CompanyTypeId_ApplicationTypeId",
                table: "CompanyTypeApplicationTypes",
                columns: new[] { "CompanyTypeId", "ApplicationTypeId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationTypeModules");

            migrationBuilder.DropTable(
                name: "CompanyTypeApplicationTypes");

            migrationBuilder.DropTable(
                name: "AppModules");

            migrationBuilder.DropTable(
                name: "ApplicationTypes");
        }
    }
}
