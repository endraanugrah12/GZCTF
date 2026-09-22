using GZCTF.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GZCTF.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260922010000_SubmissionDeletion")]
public partial class SubmissionDeletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<System.DateTimeOffset>(name: "DeletedAtUtc", table: "Submissions",
            type: "timestamp with time zone", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Submissions");
}
