using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameGlobal = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FormatCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DateFormat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    DateTimeFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PriceFormat = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
