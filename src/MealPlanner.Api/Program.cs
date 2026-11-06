using MealPlanner.Api.Common.Auth;
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

Login.Map(app);
Logout.Map(app);
GetCurrentUser.Map(app);
LoginPage.Map(app);

app.Run();
