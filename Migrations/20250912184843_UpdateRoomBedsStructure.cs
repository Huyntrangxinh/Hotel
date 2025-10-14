using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRoomBedsStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Count",
                table: "RoomBeds");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "RoomBeds");

            migrationBuilder.AddColumn<string>(
                name: "Counts",
                table: "RoomBeds",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Types",
                table: "RoomBeds",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Counts",
                table: "RoomBeds");

            migrationBuilder.DropColumn(
                name: "Types",
                table: "RoomBeds");

            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "RoomBeds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "RoomBeds",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
