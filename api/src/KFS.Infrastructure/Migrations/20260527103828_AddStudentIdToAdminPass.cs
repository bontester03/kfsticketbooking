using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KFS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentIdToAdminPass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "student_id",
                table: "admin_passes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_passes_student_id_type",
                table: "admin_passes",
                columns: new[] { "student_id", "type" },
                unique: true,
                filter: "student_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_admin_passes_students_student_id",
                table: "admin_passes",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_admin_passes_students_student_id",
                table: "admin_passes");

            migrationBuilder.DropIndex(
                name: "ix_admin_passes_student_id_type",
                table: "admin_passes");

            migrationBuilder.DropColumn(
                name: "student_id",
                table: "admin_passes");
        }
    }
}
