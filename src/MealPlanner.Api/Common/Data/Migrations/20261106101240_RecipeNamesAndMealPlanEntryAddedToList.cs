using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecipeNamesAndMealPlanEntryAddedToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AddedToListAt",
                table: "MealPlanEntries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_HouseholdId_Name",
                table: "Recipes",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_HouseholdId_Name",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "AddedToListAt",
                table: "MealPlanEntries");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes",
                column: "HouseholdId");
        }
    }
}
