using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequirementService.Migrations
{
    /// <inheritdoc />
    public partial class InitialRequirementCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProfessionalType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequirementResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementResponses_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementSkills",
                columns: table => new
                {
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementSkills", x => new { x.RequirementId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_RequirementSkills_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementResponses_RequirementId_ProfessionalId",
                table: "RequirementResponses",
                columns: new[] { "RequirementId", "ProfessionalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_CustomerId",
                table: "Requirements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_ProfessionalType_Location_Status",
                table: "Requirements",
                columns: new[] { "ProfessionalType", "Location", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementResponses");

            migrationBuilder.DropTable(
                name: "RequirementSkills");

            migrationBuilder.DropTable(
                name: "Requirements");
        }
    }
}
