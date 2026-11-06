using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("mealplanner", configureDbContextOptions: options => options
    .UseSeeding((db, _) => SeedData.Seed(db))
    .UseAsyncSeeding((db, _, ct) => SeedData.SeedAsync(db, ct)));
builder.AddCookieAuth();

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();

Login.Map(app);
Logout.Map(app);
GetCurrentUser.Map(app);
LoginPage.Map(app);

app.Run();
