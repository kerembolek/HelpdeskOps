using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpdeskOps.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DirectoryId",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DirectoryId",
                table: "AspNetUsers",
                column: "DirectoryId",
                unique: true,
                filter: "[DirectoryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DirectoryId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DirectoryId",
                table: "AspNetUsers");
        }
    }
}
