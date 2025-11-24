using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPriceIdToRoomDailyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomDailyRates_PropertyId_RoomId_Date",
                table: "RoomDailyRates");

            migrationBuilder.AddColumn<int>(
                name: "RoomPriceId",
                table: "RoomDailyRates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomDailyRates_PropertyId_RoomId_RoomPriceId_Date",
                table: "RoomDailyRates",
                columns: new[] { "PropertyId", "RoomId", "RoomPriceId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomDailyRates_PropertyId_RoomId_RoomPriceId_Date",
                table: "RoomDailyRates");

            migrationBuilder.DropColumn(
                name: "RoomPriceId",
                table: "RoomDailyRates");

            migrationBuilder.CreateIndex(
                name: "IX_RoomDailyRates_PropertyId_RoomId_Date",
                table: "RoomDailyRates",
                columns: new[] { "PropertyId", "RoomId", "Date" },
                unique: true);
        }
    }
}
