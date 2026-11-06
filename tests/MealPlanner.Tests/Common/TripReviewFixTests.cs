using System.Reflection;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Domain;
using MealPlanner.Api.Mcp;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace MealPlanner.Tests.Common;

/// <summary>Findings from the review of saved trips.</summary>
public class TripReviewFixTests
{
    [Fact]
    public void A_list_item_is_on_a_trip_at_most_once()
    {
        // Two phones pressing "Tag med på turen" at the same time must not add the item twice.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=none;Database=none;Trusted_Connection=True").Options;
        using var db = new AppDbContext(options);

        var index = db.Model.FindEntityType(typeof(ShoppingTripItem))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["TripId", "ShoppingListItemId"]));

        Assert.True(index.IsUnique);
        Assert.Equal("[ShoppingListItemId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void Active_trip_recomputes_count_and_price_from_live_quantity()
    {
        // Pia adds 2 more coffee at home; the list merges it into "4 pk.".
        var item = new ShoppingTripItem
        {
            Id = 1, Name = "Kaffe", Quantity = "2 pk.", UnitPrice = 40m, Count = 2, LinePrice = 80m,
            ShoppingListItem = new ShoppingListItem { Id = 5, Name = "Kaffe", Quantity = "4 pk." },
        };

        var dto = TripViews.ToItem(item, active: true)!;

        Assert.Equal((4, 160m), (dto.Count, dto.LinePrice));
    }

    [Fact]
    public void Creating_a_trip_from_Claude_is_marked_destructive()
    {
        // It finishes the trip in progress; Claude should use get_trip just to look.
        var tool = typeof(ShoppingListTools).GetMethod(nameof(ShoppingListTools.BuildShoppingListAsync))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.True(tool.Destructive);
    }
}
