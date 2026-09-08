using GZCTF.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GZCTF.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260907110000_AddScoreboardVisibility")]
public class AddScoreboardVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<bool>(
            name: "HideFromScoreboard", table: "AspNetUsers", type: "boolean",
            nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "HideFromScoreboard", table: "AspNetUsers");
}
