using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KFS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentRosterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "date_of_birth",
                table: "students",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "assigned_group",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_name",
                table: "students",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "student_number",
                table: "students",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_student_number",
                table: "students",
                column: "student_number",
                unique: true,
                filter: "student_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_students_student_number",
                table: "students");

            migrationBuilder.DropColumn(
                name: "assigned_group",
                table: "students");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "students");

            migrationBuilder.DropColumn(
                name: "preferred_name",
                table: "students");

            migrationBuilder.DropColumn(
                name: "student_number",
                table: "students");

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_of_birth",
                table: "students",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);
        }
    }
}
