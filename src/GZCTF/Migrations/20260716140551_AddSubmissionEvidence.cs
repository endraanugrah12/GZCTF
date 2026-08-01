using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GZCTF.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubmissionEvidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationId = table.Column<int>(type: "integer", nullable: false),
                    LlmLinks = table.Column<string>(type: "text", nullable: false),
                    SolverFileId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmissionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_Files_SolverFileId",
                        column: x => x.SolverFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_GameChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "GameChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_Participations_ParticipationId",
                        column: x => x.ParticipationId,
                        principalTable: "Participations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_ChallengeId",
                table: "SubmissionEvidence",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_GameId_ChallengeId_UserId_SubmissionId",
                table: "SubmissionEvidence",
                columns: new[] { "GameId", "ChallengeId", "UserId", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_ParticipationId",
                table: "SubmissionEvidence",
                column: "ParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_SolverFileId",
                table: "SubmissionEvidence",
                column: "SolverFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_SubmissionId",
                table: "SubmissionEvidence",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_UserId",
                table: "SubmissionEvidence",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionEvidence");

        }
    }
}
