using System.Globalization;
using MealPlanner.Api.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MealPlanner.Api.Common.Realtime;

/// <summary>
/// Pushes "the list changed" to everyone in the household (spec 4, realtime). Clients then reload the list;
/// the message carries no data, so it can never leak another household's items.
/// </summary>
[Authorize(Policy = Policies.Cookie)]
public sealed class ShoppingListHub : Hub
{
    public const string Path = "/hubs/shopping-list";
    public const string ListChanged = "ListChanged";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(Context.User!.GetHouseholdId()));
        await base.OnConnectedAsync();
    }

    public static string GroupFor(int householdId) => "household-" + householdId.ToString(CultureInfo.InvariantCulture);
}

public sealed class ListNotifier(IHubContext<ShoppingListHub> hub)
{
    public Task ListChangedAsync(int householdId, CancellationToken ct = default) =>
        hub.Clients.Group(ShoppingListHub.GroupFor(householdId)).SendAsync(ShoppingListHub.ListChanged, ct);
}
