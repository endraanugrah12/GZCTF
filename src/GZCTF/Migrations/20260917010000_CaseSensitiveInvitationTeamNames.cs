using GZCTF.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GZCTF.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917010000_CaseSensitiveInvitationTeamNames")]
public partial class CaseSensitiveInvitationTeamNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve the reservation index, but make existing reservations match
        // their original spelling. Dropping it first avoids transient collisions.
        migrationBuilder.DropIndex("IX_TeamInvitations_NormalizedTeamName", "TeamInvitations");
        migrationBuilder.Sql("UPDATE \"TeamInvitations\" SET \"NormalizedTeamName\" = \"TeamName\"");
        migrationBuilder.CreateIndex("IX_TeamInvitations_NormalizedTeamName", "TeamInvitations", "NormalizedTeamName", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The unique index deliberately rejects a downgrade while case-variant
        // reservations exist, rather than silently merging or deleting them.
        migrationBuilder.DropIndex("IX_TeamInvitations_NormalizedTeamName", "TeamInvitations");
        migrationBuilder.Sql("UPDATE \"TeamInvitations\" SET \"NormalizedTeamName\" = upper(\"TeamName\")");
        migrationBuilder.CreateIndex("IX_TeamInvitations_NormalizedTeamName", "TeamInvitations", "NormalizedTeamName", unique: true);
    }
}
