using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpdeskOps.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaBreachNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachNotifiedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaBreachNotifiedAt",
                table: "Tickets");
        }
    }
}
