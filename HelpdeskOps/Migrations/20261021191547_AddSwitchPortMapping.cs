using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpdeskOps.Migrations
{
    /// <inheritdoc />
    public partial class AddSwitchPortMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SwitchId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwitchPort",
                table: "Devices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VlanId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SwitchId_SwitchPort",
                table: "Devices",
                columns: new[] { "SwitchId", "SwitchPort" },
                unique: true,
                filter: "[SwitchId] IS NOT NULL AND [SwitchPort] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Devices_SwitchId",
                table: "Devices",
                column: "SwitchId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Devices_SwitchId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SwitchId_SwitchPort",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SwitchId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SwitchPort",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "VlanId",
                table: "Devices");
        }
    }
}
