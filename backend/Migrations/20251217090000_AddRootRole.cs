using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ucode.Backend.Data;

#nullable disable

namespace Ucode.Backend.Migrations
{
    [DbContext(typeof(UcodeDbContext))]
    [Migration("20251217090000_AddRootRole")]
    public partial class AddRootRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRoot",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRoot",
                table: "Users");
        }
    }
}
