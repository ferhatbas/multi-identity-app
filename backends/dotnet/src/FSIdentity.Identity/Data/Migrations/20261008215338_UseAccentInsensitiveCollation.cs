using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSIdentity.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseAccentInsensitiveCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "States",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "States",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "States",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "NativeName",
                table: "Languages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Languages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Languages",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "TimeZone",
                table: "Countries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PriceFormat",
                table: "Countries",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameGlobal",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FormatCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "DateTimeFormat",
                table: "Countries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "Countries",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Countries",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Cities",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Cities",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Cities",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                collation: "Latin1_General_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "States",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "States",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "States",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "NativeName",
                table: "Languages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Languages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Languages",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZone",
                table: "Countries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "PriceFormat",
                table: "Countries",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "NameGlobal",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Countries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "FormatCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "DateTimeFormat",
                table: "Countries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "DateFormat",
                table: "Countries",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Countries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Countries",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Cities",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Cities",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Cities",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldCollation: "Latin1_General_100_CI_AI");
        }
    }
}
