using Hangfire;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Jobs;
using MealPlanner.Api.Common.Tjek;
using MealPlanner.Api.Common.Web;
using MealPlanner.Api.Features.ApiKeys;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Offers;
using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Features.ShoppingList;
using MealPlanner.Api.Mcp;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("mealplanner", configureDbContextOptions: options => options
    .UseSeeding((db, _) => SeedData.Seed(db))
    .UseAsyncSeeding((db, _, ct) => SeedData.SeedAsync(db, ct)));
builder.AddAuth();
builder.AddHangfireJobs();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<TjekClient>(c => c.BaseAddress = new Uri(TjekClient.BaseAddress));
builder.Services.AddScoped<FetchOffersJob>();
builder.Services.AddScoped<SearchOffers.Handler>();
builder.Services.AddScoped<MapOffer.Handler>();
builder.Services.AddShoppingList();
builder.Services.AddRecipes();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OfferTools>()
    .WithTools<ShoppingListTools>();

var app = builder.Build();

// Before anything else touches the database, including Hangfire's schema setup below.
await app.MigrateDatabaseAsync();

app.UseForwardedHeaders();
app.UseSpaFiles();
app.UseJobsDashboardTunnelBlock();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapJobsDashboard();
FetchOffersJob.Schedule(app.Services.GetRequiredService<IRecurringJobManager>());

Login.Map(app);
Logout.Map(app);
GetCurrentUser.Map(app);
LoginPage.Map(app);
SearchOffers.Map(app);
MapOffer.Map(app);
CreateApiKey.Map(app);
ListApiKeys.Map(app);
RevokeApiKey.Map(app);
app.MapShoppingList();
app.MapRecipes();

// Claude connector: API key only (header X-Api-Key). Cloudflare restricts /mcp to Anthropic's IP ranges.
app.MapMcp("/mcp").RequireAuthorization(Policies.ApiKey);

// The Angular PWA (web/), built into wwwroot.
app.MapSpaFallback();

app.Run();
