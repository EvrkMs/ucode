using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ucode.Backend.Data;

#nullable disable

namespace Ucode.Backend.Migrations
{
    [DbContext(typeof(UcodeDbContext))]
    [Migration("20251217120000_AddCodeUserFksAndDropBalance")]
    public partial class AddCodeUserFksAndDropBalance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Codes_CreatedBy",
                table: "Codes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Codes_UsedBy",
                table: "Codes",
                column: "UsedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Codes_Users_CreatedBy",
                table: "Codes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "TelegramId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Codes_Users_UsedBy",
                table: "Codes",
                column: "UsedBy",
                principalTable: "Users",
                principalColumn: "TelegramId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Codes_Users_CreatedBy",
                table: "Codes");

            migrationBuilder.DropForeignKey(
                name: "FK_Codes_Users_UsedBy",
                table: "Codes");

            migrationBuilder.DropIndex(
                name: "IX_Codes_CreatedBy",
                table: "Codes");

            migrationBuilder.DropIndex(
                name: "IX_Codes_UsedBy",
                table: "Codes");

            migrationBuilder.AddColumn<long>(
                name: "Balance",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
