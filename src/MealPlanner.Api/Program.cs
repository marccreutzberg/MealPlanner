using MealPlanner.Api.Common.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("mealplanner", configureDbContextOptions: options => options
    .UseSeeding((db, _) => SeedData.Seed(db))
    .UseAsyncSeeding((db, _, ct) => SeedData.SeedAsync(db, ct)));

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.MapDefaultEndpoints();

app.Run();
