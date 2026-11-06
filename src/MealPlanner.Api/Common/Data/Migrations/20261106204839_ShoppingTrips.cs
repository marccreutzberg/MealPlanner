using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShoppingTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShoppingTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HouseholdId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EstimatedSaving = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingTrips_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShoppingTrips_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingTripItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    ShoppingListItemId = table.Column<int>(type: "int", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Variant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OfferId = table.Column<int>(type: "int", nullable: true),
                    OfferHeading = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LinePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LastValidDay = table.Column<DateOnly>(type: "date", nullable: true),
                    MovedByHand = table.Column<bool>(type: "bit", nullable: false),
                    WasBought = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingTripItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingTripItems_ShoppingListItems_ShoppingListItemId",
                        column: x => x.ShoppingListItemId,
                        principalTable: "ShoppingListItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShoppingTripItems_ShoppingTrips_TripId",
                        column: x => x.TripId,
                        principalTable: "ShoppingTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingTripItems_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingTripStores",
                columns: table => new
                {
                    TripId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingTripStores", x => new { x.TripId, x.StoreId });
                    table.ForeignKey(
                        name: "FK_ShoppingTripStores_ShoppingTrips_TripId",
                        column: x => x.TripId,
                        principalTable: "ShoppingTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingTripStores_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTripItems_ShoppingListItemId",
                table: "ShoppingTripItems",
                column: "ShoppingListItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTripItems_StoreId",
                table: "ShoppingTripItems",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTripItems_TripId_ShoppingListItemId",
                table: "ShoppingTripItems",
                columns: new[] { "TripId", "ShoppingListItemId" },
                unique: true,
                filter: "[ShoppingListItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTrips_CreatedByUserId",
                table: "ShoppingTrips",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTrips_HouseholdId",
                table: "ShoppingTrips",
                column: "HouseholdId",
                unique: true,
                filter: "[FinishedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingTripStores_StoreId",
                table: "ShoppingTripStores",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingTripItems");

            migrationBuilder.DropTable(
                name: "ShoppingTripStores");

            migrationBuilder.DropTable(
                name: "ShoppingTrips");
        }
    }
}
