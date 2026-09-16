using System;
using GZCTF.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GZCTF.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260916010000_TeamLeaderInvitations")]
public partial class TeamLeaderInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TeamInvitations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                TeamName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                NormalizedTeamName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProtectedToken = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                TeamId = table.Column<int>(type: "integer", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Version = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TeamInvitations", x => x.Id));
        migrationBuilder.CreateIndex("IX_TeamInvitations_NormalizedEmail", "TeamInvitations", "NormalizedEmail", unique: true);
        migrationBuilder.CreateIndex("IX_TeamInvitations_NormalizedTeamName", "TeamInvitations", "NormalizedTeamName", unique: true);
        migrationBuilder.CreateIndex("IX_TeamInvitations_TokenHash", "TeamInvitations", "TokenHash", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("TeamInvitations");
}
