using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildConnectProfileAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Website",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ProfileImageUrl",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "UserProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityStatus",
                table: "UserProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceYears",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalType",
                table: "UserProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "CUSTOMER");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalSkills",
                columns: table => new
                {
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    SkillLevel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalSkills", x => new { x.ProfessionalId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ProfessionalSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessionalSkills_UserProfiles_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "UserProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Skills",
                columns: new[] { "Id", "Category", "CreatedAtUtc", "Name" },
                values: new object[,]
                {
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a001"), "Masonry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Brick Work" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a002"), "Masonry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Concrete Work" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a003"), "Masonry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Plastering" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a004"), "Masonry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "House Construction" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a005"), "Carpentry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Furniture" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a006"), "Carpentry", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Modular Kitchen" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a007"), "Plumbing", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Bathroom Plumbing" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a008"), "Plumbing", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Kitchen Plumbing" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a009"), "Plumbing", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Water Pipeline" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a010"), "Electrical", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "House Wiring" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a011"), "Electrical", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Electrical Installation" },
                    { new Guid("1fbe1c6f-93f3-4b95-9fb1-fccf12a6a012"), "Electrical", new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Lighting" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalSkills_SkillId",
                table: "ProfessionalSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name_Category",
                table: "Skills",
                columns: new[] { "Name", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessionalSkills");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_UserProfiles_UserId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ExperienceYears",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ProfessionalType",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "UserProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "Website",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProfileImageUrl",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "UserProfiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "UserProfiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
