using Hangfire;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Tjek;
using MealPlanner.Api.Features.Offers;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Jobs;
using MealPlanner.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("mealplanner", configureDbContextOptions: options => options
    .UseSeeding((db, _) => SeedData.Seed(db))
    .UseAsyncSeeding((db, _, ct) => SeedData.SeedAsync(db, ct)));
builder.AddCookieAuth();
builder.AddHangfireJobs();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<TjekClient>(c => c.BaseAddress = new Uri(TjekClient.BaseAddress));
builder.Services.AddScoped<FetchOffersJob>();
builder.Services.AddScoped<SearchOffers.Handler>();
builder.Services.AddScoped<MapOffer.Handler>();

var app = builder.Build();

// Before anything else touches the database, including Hangfire's schema setup below.
await app.MigrateDatabaseAsync();

app.UseForwardedHeaders();
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

app.Run();
